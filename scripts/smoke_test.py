#!/usr/bin/env python3
"""End-to-end checks for the local synthetic DigitalBanking playground.

Covers the Owner/delegated/customer membership model, the all-requests-need-approval
rule, exact 100,000.00 TRY delegation boundaries, live revocation and stale JWTs,
ownership/receipt/timeline rules, reconciliation and its exception maker-checker,
the Development-only demo center, and dashboard session/role guards.

Never prints passwords, tokens, connection strings or full account numbers.
Run it against a disposable Development database only.
"""
import argparse
import base64
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from decimal import Decimal
import http.cookiejar
from html import unescape
from html.parser import HTMLParser
import json
import os
from pathlib import Path
import urllib.error
import urllib.parse
import urllib.request
import uuid

parser = argparse.ArgumentParser()
parser.add_argument("--api-port", type=int, default=5080)
parser.add_argument("--dashboard-port", type=int, default=5081)
args = parser.parse_args()
api = f"http://127.0.0.1:{args.api_port}"
dashboard = f"http://127.0.0.1:{args.dashboard_port}"
local = Path(__file__).resolve().parents[1] / ".local"
credentials = json.loads((local / "credentials.json").read_text())
owner_username = os.environ.get("SMOKE_OWNER_USERNAME") or credentials.get("owner_username")
owner_password = os.environ.get("SMOKE_OWNER_PASSWORD") or credentials.get("owner_password")
if not owner_username or not owner_password:
    raise SystemExit("Owner credentials are missing. Complete local /Setup and store owner_username/owner_password in .local/credentials.json.")

token = None
checks = 0


def check(condition, message):
    global checks
    assert condition, message
    checks += 1


def request(path, body=None, expected=200, auth=True, token_override=None, headers=None,
            return_response=False, method=None, timeout=30):
    """Call the local API; expected may be an int or a collection of ints."""
    global checks
    request_headers = {"Content-Type": "application/json"}
    if auth:
        use_token = token_override if token_override is not None else token
        if use_token:
            request_headers["Authorization"] = "Bearer " + use_token
    if headers:
        request_headers.update(headers)
    verb = method or ("POST" if body is not None else "GET")
    req = urllib.request.Request(
        api + path,
        data=json.dumps(body).encode() if body is not None else None,
        headers=request_headers,
        method=verb,
    )
    try:
        response = urllib.request.urlopen(req, timeout=timeout)
    except urllib.error.HTTPError as error:
        response = error
    payload = response.read()
    expected_codes = expected if isinstance(expected, (set, tuple, list)) else {expected}
    assert response.code in expected_codes, f"{path}: expected {sorted(expected_codes)}, got {response.code}"
    checks += 1
    parsed = json.loads(payload) if payload else None
    return (parsed, response) if return_response else parsed


def login(username, password, expected=200):
    data = request("/api/auth/login", {"username": username, "password": password},
                   expected=expected, auth=False)
    if expected == 200:
        assert data["success"] and data["data"]["token"], "login did not return a token"
        return data["data"]["token"]
    return data


def token_claims(value):
    payload = value.split(".")[1]
    payload += "=" * (-len(payload) % 4)
    return json.loads(base64.urlsafe_b64decode(payload))


def claim(value, name):
    return value.get(f"http://schemas.microsoft.com/ws/2008/06/identity/claims/{name}") or value.get(name)


def accounts_for(auth_token):
    return request("/api/me/accounts", token_override=auth_token)["data"]


def account_balance(account_id, auth_token):
    data = request(f"/api/me/accounts/{account_id}", token_override=auth_token)["data"]
    return Decimal(str(data["balance"]))


def pending_request(customer_token, from_id, to_id, amount, description, key=None):
    headers = {"Idempotency-Key": key or ("smoke-transfer-" + uuid.uuid4().hex)}
    return request("/api/me/transfers", {
        "fromAccountId": from_id,
        "toAccountId": to_id,
        "amount": amount,
        "description": description,
    }, expected=201, token_override=customer_token, headers=headers)["data"]


def approve(payment_id, approver_token, expected=204):
    return request(f"/api/paymentapprovals/{payment_id}/approve", method="POST",
                   expected=expected, token_override=approver_token)


def reject(payment_id, approver_token, reason, expected=204):
    return request(f"/api/paymentapprovals/{payment_id}/reject",
                   {"rejectionReason": reason}, expected=expected,
                   token_override=approver_token)


def upload_csv(content, auth_token, filename="statement.csv", expected=200, query=""):
    boundary = "----smoke" + uuid.uuid4().hex
    body = (
        f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{filename}\"\r\n"
        "Content-Type: text/csv\r\n\r\n"
    ).encode() + content + f"\r\n--{boundary}--\r\n".encode()
    req = urllib.request.Request(
        api + "/api/reconciliation/preview" + query, data=body, method="POST",
        headers={"Authorization": "Bearer " + auth_token,
                 "Content-Type": f"multipart/form-data; boundary={boundary}"})
    try:
        response = urllib.request.urlopen(req, timeout=30)
    except urllib.error.HTTPError as error:
        response = error
    payload = response.read()
    expected_codes = expected if isinstance(expected, (set, tuple, list)) else {expected}
    assert response.code in expected_codes, f"preview: expected {sorted(expected_codes)}, got {response.code}"
    global checks
    checks += 1
    return json.loads(payload)


def get_text(path, auth_token):
    req = urllib.request.Request(api + path, headers={"Authorization": "Bearer " + auth_token})
    response = urllib.request.urlopen(req, timeout=30)
    global checks
    checks += 1
    return response.read().decode("utf-8-sig")


# --- Health, setup and Owner identity --------------------------------------
check(request("/health", auth=False)["status"] == "ok", "health failed")
request("/api/accounts", expected=401, auth=False)
request("/api/auth/login", {"username": "nobody-smoke", "password": "WrongPass1"},
        expected=400, auth=False)

setup = request("/api/setup/status", auth=False)
active_owner_username = owner_username
setup_posts = 0


def raw_owner_attempt(username):
    body = {
        "username": username,
        "email": f"{username}@demo.invalid",
        "firstName": "Yarış",
        "lastName": "Owner",
        "password": owner_password,
        "confirmPassword": owner_password,
    }
    req = urllib.request.Request(
        api + "/api/setup/owner",
        data=json.dumps(body).encode(),
        headers={"Content-Type": "application/json"},
        method="POST")
    try:
        response = urllib.request.urlopen(req, timeout=30)
        payload = response.read()
        return response.code, (json.loads(payload) if payload else None)
    except urllib.error.HTTPError as error:
        payload = error.read()
        return error.code, (json.loads(payload) if payload else None)


if setup["needsOwner"]:
    # Two real HTTP requests, two different usernames, one empty database.
    # The DB-level filtered unique index must let exactly one win; the other
    # must receive the domain 409, never a 500.
    race_names = [owner_username, owner_username + "-race"]
    with ThreadPoolExecutor(max_workers=2) as pool:
        race_results = list(pool.map(raw_owner_attempt, race_names))
    setup_posts = 2
    codes = sorted(code for code, _ in race_results)
    check(codes == [200, 409], f"concurrent Owner setup returned {codes}, expected [200, 409]")
    winner = next(payload for code, payload in race_results if code == 200)
    loser = next(payload for code, payload in race_results if code == 409)
    check(winner["data"]["role"] == "Owner", "winning setup did not create Owner")
    check("Owner zaten kurulmuş" in loser["message"], "losing setup did not explain the closed endpoint")
    active_owner_username = winner["data"]["username"]
else:
    # A repeat setup must be closed once an Owner exists.
    request("/api/setup/owner", {
        "username": "second-owner-" + uuid.uuid4().hex[:6],
        "email": "second-owner@demo.invalid",
        "firstName": "Second",
        "lastName": "Owner",
        "password": "SyntheticOwner2",
        "confirmPassword": "SyntheticOwner2",
    }, expected=409, auth=False)
    setup_posts = 1

status = request("/api/setup/status", auth=False)
check(status["needsOwner"] is False, "setup status still needs an Owner")

token = login(active_owner_username, owner_password)
owner_data = request("/api/auth/me")["data"]
owner_id = owner_data["userId"]
check(owner_data["role"] == "Owner", "Owner claims role mismatch")
check(claim(token_claims(token), "role") == "Owner", "JWT role is not Owner")

request("/api/users")
request("/api/customers")
request("/api/accounts")
request("/api/audit?pageSize=10")
request("/api/delegations")
request("/api/demo/scenarios")
request("/swagger/v1/swagger.json", auth=False)
request("/api/accounts", token_override="not.a.real.token", expected=401)

# --- Registration, activation, role boundaries ----------------------------
suffix = uuid.uuid4().hex[:8]
customer_name = "smoke.customer." + suffix
customer_password = "SyntheticCust1"
approver_name = "smoke.approver." + suffix
approver_password = "SyntheticAppr1"

registered = request("/api/auth/register", {
    "username": customer_name,
    "email": f"{customer_name}@demo.invalid",
    "firstName": "Selin",
    "lastName": "Smoke",
    "password": customer_password,
    "confirmPassword": customer_password,
}, auth=False)["data"]
customer_id = registered["userId"]
check(registered["status"] == "PendingActivation", "public registration must be pending")

# The client cannot choose Owner or any role; the extra field is ignored.
registered_approver = request("/api/auth/register", {
    "username": approver_name,
    "email": f"{approver_name}@demo.invalid",
    "firstName": "Kerem",
    "lastName": "Smoke",
    "password": approver_password,
    "confirmPassword": approver_password,
    "role": "Owner",
}, auth=False)["data"]
approver_id = registered_approver["userId"]

request("/api/auth/register", {
    "username": customer_name,
    "email": f"other-{customer_name}@demo.invalid",
    "firstName": "Duplicate",
    "lastName": "Smoke",
    "password": customer_password,
    "confirmPassword": customer_password,
}, expected=409, auth=False)
request("/api/auth/register", {
    "username": "bad." + suffix,
    "email": "not-an-email",
    "firstName": "Bad",
    "lastName": "Smoke",
    "password": "short",
    "confirmPassword": "different",
}, expected=400, auth=False)

login(customer_name, customer_password, expected=403)
login(approver_name, approver_password, expected=403)

request(f"/api/users/{customer_id}/activate", method="POST")
request(f"/api/users/{approver_id}/activate", method="POST")
request(f"/api/users/{approver_id}/role", {"role": "DelegatedApprover"}, method="POST")
request(f"/api/users/{owner_id}/disable", method="POST", expected=400)
request(f"/api/users/{owner_id}/role", {"role": "Customer"}, method="POST", expected=400)

customer_token = login(customer_name, customer_password)
approver_token = login(approver_name, approver_password)
customer_me = request("/api/auth/me", token_override=customer_token)["data"]
approver_me = request("/api/auth/me", token_override=approver_token)["data"]
check(customer_me["role"] == "Customer", "activated applicant is not Customer")
check(approver_me["role"] == "DelegatedApprover", "role change did not apply")
check(customer_me["userId"] == customer_id and approver_me["userId"] == approver_id,
      "JWT user ids do not match the registered users")

# The role field in the registration payload was ignored.
request("/api/users", token_override=approver_token, expected=403)
request("/api/users", token_override=customer_token, expected=403)
request("/api/delegations", token_override=approver_token, expected=403)
request(f"/api/users/{customer_id}/role", {"role": "Owner"}, method="POST",
        token_override=approver_token, expected=403)
request("/api/paymentapprovals/requests", {
    "requestType": "Transfer", "accountId": str(uuid.uuid4()),
    "toAccountId": str(uuid.uuid4()), "amount": 1,
}, expected=403, token_override=approver_token)
request("/api/accounts/transfer", {"fromAccountId": str(uuid.uuid4()), "toAccountId": str(uuid.uuid4()), "amount": 1},
        expected=403, token_override=customer_token)

# --- Delegations: inclusive 100,000.00 TRY boundary -----------------------
request("/api/delegations", {
    "userId": approver_id,
    "operation": "Transfer",
    "currency": "TRY",
    "maxAmount": 100000.00,
}, method="POST")
delegations = request("/api/delegations?userId=" + str(approver_id))["data"]
check(any(d["operation"] == "Transfer" and d["currency"] == "TRY"
          and Decimal(str(d["maxAmount"])) == Decimal("100000.00") for d in delegations),
      "transfer delegation limit was not created")
request("/api/delegations", {
    "userId": approver_id,
    "operation": "Deposit",
    "currency": "TRY",
    "maxAmount": 100000.00,
    "dailyLimit": 50000.00,
}, method="POST")
request("/api/delegations", {
    "userId": approver_id,
    "operation": "ReconciliationException",
    "maxAmount": None,
}, method="POST")
request("/api/delegations", {
    "userId": customer_id, "operation": "Transfer", "currency": "TRY", "maxAmount": 1,
}, method="POST", expected=400)

# --- Customer account opening and all-request approval flow ---------------
profile = request("/api/me/profile", token_override=customer_token)["data"]
check(profile["firstName"] == "Selin", "activated profile was not created")
request("/api/me/accounts", token_override=customer_token)

opening = request("/api/me/requests", {
    "requestType": "AccountOpening",
    "currency": "TRY",
    "amount": 0,
    "description": "Synthetic smoke account opening",
}, expected=201, token_override=customer_token)["data"]
check(opening["status"] == "Pending" and opening["requestType"] == "AccountOpening",
      "account opening was not left pending")
check(opening["createdAccountId"] is None, "account was created before approval")
approve(opening["id"], token)
opening_detail = request(f"/api/paymentapprovals/{opening['id']}")["data"]
check(opening_detail["status"] == "Approved" and opening_detail["createdAccountId"],
      "approved account opening did not create an account")
new_account_id = opening_detail["createdAccountId"]

accounts = request("/api/me/accounts", token_override=customer_token)["data"]
check(any(a["id"] == new_account_id for a in accounts), "new account is not visible to its owner")
check(account_balance(new_account_id, customer_token) == Decimal("0.00"),
      "new account is not empty before deposit")

deposit = request("/api/me/requests", {
    "requestType": "Deposit",
    "accountId": new_account_id,
    "amount": 300000.00,
    "description": "Synthetic funding deposit",
}, expected=201, token_override=customer_token)["data"]
check(deposit["status"] == "Pending", "deposit was auto-approved")
check(account_balance(new_account_id, customer_token) == Decimal("0.00"),
      "deposit moved money at request creation")
approve(deposit["id"], token)
check(account_balance(new_account_id, customer_token) == Decimal("300000.00"),
      "approved deposit did not post exactly once")

withdrawal = request("/api/me/requests", {
    "requestType": "Withdrawal",
    "accountId": new_account_id,
    "amount": 100.00,
    "description": "Synthetic withdrawal",
}, expected=201, token_override=customer_token)["data"]
check(withdrawal["status"] == "Pending", "withdrawal bypassed approval")
approve(withdrawal["id"], token)
check(account_balance(new_account_id, customer_token) == Decimal("299900.00"),
      "approved withdrawal did not post exactly once")

# --- Ownership and IDOR ----------------------------------------------------
seeded_a = login("customer.a", credentials["password"])
customer_a_accounts = request("/api/me/accounts", token_override=seeded_a)["data"]
seeded_a_try = next(a["id"] for a in customer_a_accounts if a["currency"] == "TRY")
request(f"/api/me/accounts/{seeded_a_try}", expected=404, token_override=customer_token)
request(f"/api/me/accounts/{new_account_id}", expected=404, token_override=seeded_a)

owner_accounts = request("/api/accounts?pageSize=100")["data"]["items"]
a_usd = next(a["id"] for a in owner_accounts if a["accountNumber"] == "1000000002")
b_usd = next(a["id"] for a in owner_accounts if a["accountNumber"] == "2000000002")
other_currency_pending = pending_request(seeded_a, a_usd, b_usd, 100,
                                         "Synthetic USD delegation boundary")
request(f"/api/paymentapprovals/{other_currency_pending['id']}", expected=404,
        token_override=customer_token)
approve(other_currency_pending["id"], approver_token, expected=400)
reject(other_currency_pending["id"], token, "Delegation is TRY-only; Owner rejects synthetic USD request.")

# --- 100,000.00 TRY inclusive, 100,000.01 Owner-only ----------------------
to_b = next(a["id"] for a in owner_accounts if a["accountNumber"] == "2000000001")

over = pending_request(customer_token, new_account_id, to_b, 100000.01,
                       "Synthetic 100000.01 boundary")
approver_queue = request("/api/paymentapprovals/queue?status=Pending", token_override=approver_token)["data"]
check(all(item["id"] != over["id"] for item in approver_queue),
      "over-limit 100000.01 request leaked into delegated queue")
approve(over["id"], approver_token, expected=400)
check(request(f"/api/paymentapprovals/{over['id']}")["data"]["status"] == "Pending",
      "denied delegated approval changed request state")
reject(over["id"], token, "Synthetic 100000.01 is above delegated limit.")

exact_lower = pending_request(customer_token, new_account_id, to_b, 99999.99,
                              "Synthetic 99999.99 boundary")
check(any(item["id"] == exact_lower["id"] for item in
          request("/api/paymentapprovals/queue?status=Pending", token_override=approver_token)["data"]),
      "99999.99 request was not eligible for delegated queue")
approve(exact_lower["id"], approver_token)
check(account_balance(new_account_id, customer_token) == Decimal("199900.01"),
      "99999.99 approval did not post exactly")

exact_limit = pending_request(customer_token, new_account_id, to_b, 100000.00,
                              "Synthetic 100000.00 inclusive boundary")
approve(exact_limit["id"], approver_token)
check(account_balance(new_account_id, customer_token) == Decimal("99900.01"),
      "100000.00 inclusive approval did not post exactly")

# The previous request must not be approvable twice.
approve(exact_limit["id"], approver_token, expected=400)
check(account_balance(new_account_id, customer_token) == Decimal("99900.01"),
      "double approval moved money twice")

# Reserve-aware pending protection: 60k pending leaves 39,900.01 available.
overcommit = pending_request(customer_token, new_account_id, to_b, 60000,
                             "Synthetic reserve 60000")
request("/api/me/transfers", {
    "fromAccountId": new_account_id, "toAccountId": to_b,
    "amount": 50000, "description": "Synthetic second overcommit",
}, expected=400, token_override=customer_token,
    headers={"Idempotency-Key": "smoke-overcommit-" + uuid.uuid4().hex})
request(f"/api/me/payments/{overcommit['id']}/cancel", method="POST",
        expected=204, token_override=customer_token)
check(request(f"/api/me/payments/{overcommit['id']}", token_override=customer_token)["data"]["status"] == "Cancelled",
      "customer cancellation did not complete")

# Low risk is still not auto-approved.
low = pending_request(customer_token, new_account_id, to_b, 1.00,
                      "Synthetic low-risk requires approval")
check(low["status"] == "Pending" and low["riskLevel"] == "Low",
      "low-risk request no longer requires approval")
request(f"/api/me/payments/{low['id']}/cancel", method="POST", expected=204,
        token_override=customer_token)

# --- Daily cumulative delegation limit (deposit) --------------------------
daily_one = request("/api/me/requests", {
    "requestType": "Deposit", "accountId": new_account_id, "amount": 40000,
    "description": "Synthetic daily limit one",
}, expected=201, token_override=customer_token)["data"]
approve(daily_one["id"], approver_token)
check(account_balance(new_account_id, customer_token) == Decimal("139900.01"),
      "first daily deposit did not post")
daily_two = request("/api/me/requests", {
    "requestType": "Deposit", "accountId": new_account_id, "amount": 20000,
    "description": "Synthetic daily limit two",
}, expected=201, token_override=customer_token)["data"]
approve(daily_two["id"], approver_token, expected=400)
reject(daily_two["id"], token, "Synthetic daily 50k limit reached; Owner rejects.")

# --- Revocation is live, even with an old JWT -----------------------------
revoked_payment = pending_request(customer_token, new_account_id, to_b, 1000,
                                  "Synthetic revocation request")
transfer_delegation = next(d for d in request("/api/delegations?userId=" + str(approver_id))["data"]
                           if d["operation"] == "Transfer")
request(f"/api/delegations/{transfer_delegation['id']}/deactivate", method="POST")
approve(revoked_payment["id"], approver_token, expected=400)
check(request(f"/api/paymentapprovals/{revoked_payment['id']}")["data"]["status"] == "Pending",
      "revoked delegation still approved a pending request")
request(f"/api/me/payments/{revoked_payment['id']}/cancel", method="POST", expected=204,
        token_override=customer_token)

# --- Owner self-approval block, customer rejection, stale JWT role change -
owner_self = request("/api/paymentapprovals", {
    "fromAccountId": seeded_a_try, "toAccountId": to_b,
    "amount": 500, "description": "Synthetic Owner self approval",
}, expected=201)["data"]
approve(owner_self["id"], token, expected=400)
reject(owner_self["id"], token, "Owner cannot approve own request; cancel instead.",
       expected=400)
request(f"/api/paymentapprovals/{owner_self['id']}/cancel", method="POST", expected=204)

rejected_payment = pending_request(customer_token, new_account_id, to_b, 750,
                                    "Synthetic customer rejection")
reject(rejected_payment["id"], token, "Synthetic customer rejection reason")
customer_view = request(f"/api/me/payments/{rejected_payment['id']}", token_override=customer_token)["data"]
check(customer_view["status"] == "Rejected" and
      customer_view["rejectionReason"] == "Synthetic customer rejection reason",
      "customer cannot see the rejection reason")

# --- Direct movement endpoints cannot bypass approvals --------------------
before_bypass = account_balance(new_account_id, customer_token)
request("/api/transactions/deposit", {"accountId": new_account_id, "amount": 10}, expected=400)
request("/api/transactions/withdraw", {"accountId": new_account_id, "amount": 10}, expected=400)
request("/api/accounts/transfer", {
    "fromAccountId": new_account_id, "toAccountId": to_b, "amount": 10,
}, expected=400)
request("/api/accounts/open", {"customerId": customer_id, "currency": "TRY"}, expected=400)
check(account_balance(new_account_id, customer_token) == before_bypass,
      "direct movement endpoint changed the balance")

# --- Timeline, status-gated receipt and masking ---------------------------
timeline = request(f"/api/me/payments/{exact_limit['id']}/timeline",
                   token_override=customer_token)["data"]
types = {item["type"] for item in timeline["events"]}
check({"Created", "Pending", "Approved", "Completed"} <= types,
      "completed payment timeline is incomplete")
receipt = request(f"/api/me/payments/{exact_limit['id']}/receipt",
                  token_override=customer_token)["data"]
check(receipt["isCompleted"] is True and receipt["completedAtLocal"] is not None,
      "completed receipt is not complete")
check(receipt["fromAccountMasked"].startswith("••••") and
      receipt["toAccountMasked"].startswith("••••"), "receipt account numbers are not masked")
check("DEMO" in receipt["disclaimer"] and receipt["timeZoneId"] == "Europe/Istanbul",
      "receipt disclaimer/timezone missing")
request(f"/api/me/payments/{exact_limit['id']}/receipt", expected=404, token_override=seeded_a)
pending_receipt = request(f"/api/me/payments/{rejected_payment['id']}/receipt",
                          token_override=customer_token)["data"]
check(pending_receipt["isCompleted"] is False and pending_receipt["completedAtLocal"] is None,
      "non-completed payment produced a completed receipt")

# --- Reconciliation: preview/import, replay, export, exceptions -----------
request("/api/reconciliation/batches", token_override=customer_token, expected=403)
request("/api/reconciliation/batches", token_override=approver_token, expected=403)

request("/api/demo/seed", method="POST")
same_seed = request("/api/demo/seed", method="POST")["data"]
check(same_seed["alreadySeeded"] is True, "demo seed is not idempotent")
scenarios = request("/api/demo/scenarios")["data"]
check(len(scenarios) >= 7, "demo scenario catalog is incomplete")
request("/api/demo/scenarios", token_override=customer_token, expected=403)

matched_csv = get_text("/api/demo/reconciliation-sample?kind=matched", token).encode()
preview = upload_csv(matched_csv, token)
batch_id = preview["data"]["batch"]["id"]
check(preview["data"]["batch"]["status"] == "Previewed", "matched sample was not previewed")
replayed_preview = upload_csv(matched_csv, token)
check(replayed_preview["data"]["replayed"] is True and
      replayed_preview["data"]["batch"]["id"] == batch_id,
      "duplicate upload was not detected by file fingerprint")
imported = request(f"/api/reconciliation/{batch_id}/import", method="POST")["data"]
check(imported["batch"]["status"] == "Imported" and imported["batch"]["matchedCount"] >= 2,
      "matched import did not reconcile the demo transfer")
check(request(f"/api/reconciliation/{batch_id}/import", method="POST")["data"]["batch"]["status"] == "Imported",
      "repeated import is not idempotent")

mismatch_csv = get_text("/api/demo/reconciliation-sample?kind=mismatch", token).encode()
mismatch_preview = upload_csv(mismatch_csv, token)
mismatch_batch = mismatch_preview["data"]["batch"]
mismatch_detail = request(f"/api/reconciliation/{mismatch_batch['id']}/import", method="POST")["data"]
check(mismatch_detail["batch"]["amountMismatchCount"] >= 2 and
      mismatch_detail["batch"]["onlyExternalCount"] >= 1 and
      mismatch_detail["batch"]["duplicateReferenceCount"] >= 2,
      "mismatch sample did not produce deterministic differences")
only_external = next(row for row in mismatch_detail["rows"]
                     if row["matchStatus"] == "OnlyExternal")
balance_before_exception = account_balance(new_account_id, customer_token)
exception = request("/api/reconciliation/exceptions", {
    "rowId": only_external["id"],
    "description": "Synthetic operational mismatch exception",
}, expected=201)["data"]
check(exception["status"] == "Open", "exception did not open")
request(f"/api/reconciliation/exceptions/{exception['id']}/review", method="POST")
request(f"/api/reconciliation/exceptions/{exception['id']}/propose",
        {"resolutionNote": "Synthetic operational note; no balance change."}, method="POST")
request(f"/api/reconciliation/exceptions/{exception['id']}/approve", method="POST", expected=400)
closed = request(f"/api/reconciliation/exceptions/{exception['id']}", token_override=approver_token)["data"]
check(closed["status"] == "ResolutionProposed", "self-approval changed exception state")
request(f"/api/reconciliation/exceptions/{exception['id']}/approve", method="POST",
        token_override=approver_token)
closed = request(f"/api/reconciliation/exceptions/{exception['id']}")["data"]
check(closed["status"] == "Closed", "delegated exception approval did not close")
check(account_balance(new_account_id, customer_token) == balance_before_exception,
      "exception close adjusted a balance")
request(f"/api/reconciliation/exceptions/{exception['id']}/reopen", {"reason": "Synthetic reopen"},
        method="POST")
check(request(f"/api/reconciliation/exceptions/{exception['id']}")["data"]["status"] == "Open",
      "reopen did not restore Open")

export_text = get_text(f"/api/reconciliation/batches/{mismatch_batch['id']}/export", token)
check("line_number,external_reference" in export_text, "reconciliation export header missing")
formula_csv = (
    "external_reference,internal_reference,amount,currency,transaction_date,direction\n"
    f"=2+2,{uuid.uuid4()},12.34,TRY,{datetime.now(timezone.utc).date().isoformat()},Debit\n"
).encode()
formula_preview = upload_csv(formula_csv, token, filename="formula.csv")
formula_import = request(f"/api/reconciliation/{formula_preview['data']['batch']['id']}/import",
                         method="POST")["data"]
formula_export = get_text(f"/api/reconciliation/batches/{formula_import['batch']['id']}/export", token)
check("'=2+2" in formula_export, "CSV export is not formula-injection safe")

invalid = upload_csv(b"foo,bar\n1,2\n", token, filename="invalid.csv")
check(invalid["success"] is False and invalid["data"]["batch"]["hasErrors"] is True,
      "invalid CSV was accepted")
request(f"/api/reconciliation/{invalid['data']['batch']['id']}/import", method="POST", expected=400)
upload_csv(b"foo", token, filename="invalid.txt", expected=400)
upload_csv(b"", token, filename="empty.csv", expected=400)

# --- Dashboard: role-specific navigation and guards -----------------------
class AntiForgeryParser(HTMLParser):
    value = None

    def handle_starttag(self, tag, attrs):
        data = dict(attrs)
        if tag == "input" and data.get("name") == "__RequestVerificationToken":
            self.value = data.get("value")


def dashboard_login(username, password, expected_path):
    browser = urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    page = browser.open(dashboard + "/Login", timeout=30)
    form = AntiForgeryParser()
    form.feed(page.read().decode())
    check(form.value is not None, "dashboard login antiforgery token missing")
    payload = urllib.parse.urlencode({
        "Username": username,
        "Password": password,
        "__RequestVerificationToken": form.value,
    }).encode()
    page = browser.open(urllib.request.Request(dashboard + "/Login", data=payload), timeout=30)
    path = urllib.parse.urlparse(page.url).path
    check(path == expected_path, f"dashboard login landed on {path}, expected {expected_path}")
    return browser


def dashboard_page(browser, path, expected=200, expected_path=None):
    try:
        page = browser.open(dashboard + path, timeout=30)
    except urllib.error.HTTPError as error:
        check(error.code == expected, f"{path}: expected {expected}, got {error.code}")
        return ""
    actual_path = urllib.parse.urlparse(page.url).path
    check(page.status == expected, f"{path}: expected {expected}, got {page.status}")
    if expected_path is not None:
        check(actual_path == expected_path, f"{path} landed on {actual_path}, expected {expected_path}")
    return page.read().decode()


anonymous = urllib.request.build_opener(
    urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
anonymous_page = anonymous.open(dashboard + "/Admin/Users/Index", timeout=30)
check(urllib.parse.urlparse(anonymous_page.url).path == "/Login",
      "anonymous dashboard admin page did not require login")

# A stale Setup form must not submit after the one-time Owner exists.
setup_browser = urllib.request.build_opener(
    urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
stale_setup_page = setup_browser.open(dashboard + "/Setup", timeout=30)
stale_html = stale_setup_page.read().decode()
stale_path = urllib.parse.urlparse(stale_setup_page.url)
check(stale_path.path == "/Login" and "setup" in urllib.parse.parse_qs(stale_path.query),
      "completed Setup GET did not redirect to the login notice")
stale_form = AntiForgeryParser()
stale_form.feed(stale_html)
check(stale_form.value is not None, "stale Setup page had no antiforgery token")
stale_payload = urllib.parse.urlencode({
    "Username": "late-owner-" + suffix,
    "Email": f"late-owner-{suffix}@demo.invalid",
    "FirstName": "Late",
    "LastName": "Owner",
    "Password": "SyntheticOwner1",
    "ConfirmPassword": "SyntheticOwner1",
    "__RequestVerificationToken": stale_form.value,
}).encode()
stale_result = setup_browser.open(
    urllib.request.Request(dashboard + "/Setup", data=stale_payload), timeout=30)
stale_result_html = stale_result.read().decode()
stale_result_path = urllib.parse.urlparse(stale_result.url)
check(stale_result_path.path == "/Login" and "setup" in urllib.parse.parse_qs(stale_result_path.query),
      "stale Setup POST did not redirect to login")
check("Too many" not in stale_result_html and "Owner oluşturuldu" in unescape(stale_result_html),
      "stale Setup POST did not show the Turkish completed-setup notice")
setup_browser.open(dashboard + "/Logout", timeout=30)

owner_browser = dashboard_login(active_owner_username, owner_password, "/Admin/Overview")
for path in ["/Admin/Overview", "/Admin/Approvals/Index", "/Admin/Users/Index",
             "/Admin/Delegations/Index", "/Admin/Customers/Index", "/Admin/Requests/New",
             "/Admin/Audit/Index", "/Admin/Reconciliation/Index", "/Admin/Demo/Index",
             "/Reports/TopAccounts"]:
    dashboard_page(owner_browser, path)

approver_browser = dashboard_login(approver_name, approver_password, "/Admin/Approvals")
dashboard_page(approver_browser, "/Admin/Approvals/Index")
denied_approver = dashboard_page(approver_browser, "/Admin/Users/Index", expected_path="/Admin/Approvals")
check(denied_approver != "", "delegated approver saw an empty page")

customer_browser = dashboard_login(customer_name, customer_password, "/Customer/Home")
for path in ["/Customer/Home", "/Customer/Accounts/Index", "/Customer/Transfer",
             "/Customer/Requests", "/Customer/Payments/Index"]:
    dashboard_page(customer_browser, path)
dashboard_page(customer_browser, "/Admin/Approvals/Index", expected_path="/Customer/Home")
dashboard_page(owner_browser, "/Customer/Home", expected_path="/Admin/Overview")

for browser in (owner_browser, approver_browser, customer_browser):
    browser.open(dashboard + "/Logout", timeout=30)
    after_logout = browser.open(dashboard + "/Customer/Home", timeout=30)
    check(urllib.parse.urlparse(after_logout.url).path == "/Login",
          "dashboard logout did not end the session")

# A role reduction is live immediately; the old JWT keeps its claim but no authority.
stale_payment = pending_request(customer_token, new_account_id, to_b, 2000,
                                "Synthetic stale JWT request")
request(f"/api/users/{approver_id}/role", {"role": "Customer"}, method="POST")
approve(stale_payment["id"], approver_token, expected=403)
check(request(f"/api/paymentapprovals/{stale_payment['id']}")["data"]["status"] == "Pending",
      "old JWT still approved after role change")
request(f"/api/me/payments/{stale_payment['id']}/cancel", method="POST", expected=204,
        token_override=customer_token)

# --- Audit, reset and login throttle --------------------------------------
audit = request("/api/audit?pageSize=200")["data"]
actions = {item["action"] for item in audit["items"]}
required_actions = {
    "OWNER_SETUP", "MEMBERSHIP_ACTIVATED", "USER_ROLE_CHANGED",
    "DELEGATION_CREATED", "DELEGATION_DEACTIVATED",
    "APPROVAL_REQUEST_CREATED", "APPROVAL_REQUEST_APPROVED",
    "APPROVAL_REQUEST_REJECTED", "APPROVAL_REQUEST_CANCELLED",
    "RECONCILIATION_IMPORTED", "RECON_EXCEPTION_CLOSED", "DEMO_SEEDED",
}
missing = required_actions - actions
check(not missing, f"audit is missing actions: {sorted(missing)}")
audit_text = json.dumps(audit).lower()
for secret in (owner_password, customer_password, approver_password, credentials["password"], token):
    check(secret.lower() not in audit_text, "audit leaked a password or token")

reset = request("/api/demo/reset", method="POST")["data"]
check(reset["removedPayments"] >= 3,
      "demo reset did not remove scoped demo payments")
check(reset["removedBatches"] >= 0, "demo reset returned an invalid batch count")
check(request("/api/demo/scenarios")["data"][0]["relatedPaymentId"] is None,
      "demo reset left scenario payment links")
check(account_balance(new_account_id, customer_token) == Decimal("139900.01"),
      "demo reset touched non-demo UAT account")

# Setup has its own 5/10-minute partition. This must be a Turkish 429 with
# Retry-After, not the generic English login message and not an unlimited bypass.
remaining_setup_posts = max(6 - setup_posts, 1)
for _ in range(remaining_setup_posts - 1):
    request("/api/setup/owner", {
        "username": "post-setup-" + uuid.uuid4().hex[:6],
        "email": "post-setup@demo.invalid",
        "firstName": "Post",
        "lastName": "Setup",
        "password": "SyntheticOwner1",
        "confirmPassword": "SyntheticOwner1",
    }, expected=409, auth=False)
rate_payload, rate_response = request("/api/setup/owner", {
    "username": "post-setup-" + uuid.uuid4().hex[:6],
    "email": "post-setup@demo.invalid",
    "firstName": "Post",
    "lastName": "Setup",
    "password": "SyntheticOwner1",
    "confirmPassword": "SyntheticOwner1",
}, expected=429, auth=False, return_response=True)
check("kurulum" in rate_payload["message"].lower(), "setup 429 message is not Turkish")
check("Too many" not in rate_payload["message"], "setup 429 leaked the old English message")
check(rate_response.headers.get("Retry-After") == "600",
      "setup 429 did not return Retry-After: 600")
check(rate_response.headers.get("Content-Type", "").startswith("application/json"),
      "setup 429 content type is not JSON")

lock_name = "lockout." + uuid.uuid4().hex[:8]
for _ in range(5):
    login(lock_name, "WrongPass1", expected=400)
login(lock_name, "WrongPass1", expected=429)

print(
    f"PASS: {checks} local checks: concurrent Owner setup (200/409), stale Setup "
    "POST/login notice, setup 429 Retry-After, membership activation, "
    "Customer/DelegatedApprover/Owner boundaries, all-request approval, "
    "99,999.99/100,000.00/100,000.01 TRY, other currency, daily limit, "
    "reserve-aware overcommit, live revocation/stale JWT, owner self-approval block, "
    "IDOR, timeline/receipt/masking, reconciliation replay/match/mismatch/exceptions, "
    "formula-safe export, demo seed/reset, audit and dashboard role/session flows."
)
