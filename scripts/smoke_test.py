#!/usr/bin/env python3
"""Exercise only the local synthetic playground. Never prints tokens or passwords.

Baseline scenarios (health, authorization, login, Swagger, customers, accounts,
decimal balances, transfer, rejection paths, reports, dashboard session) are kept
and extended with role/claim, maker-checker, double-approval, idempotency,
currency, audit and dashboard role/session checks.
"""
import argparse
import base64
from datetime import datetime, timedelta, timezone
from decimal import Decimal
import http.cookiejar
from html.parser import HTMLParser
import json
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
credentials = json.loads((Path(__file__).resolve().parents[1] / ".local/credentials.json").read_text())
token = None
checks = 0


def request(path, body=None, expected=200, auth=True, token_override=None, headers=None,
            return_response=False, method=None):
    """Call the local API. Never prints request/response bodies containing secrets."""
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
        response = urllib.request.urlopen(req, timeout=30)
    except urllib.error.HTTPError as error:
        response = error
    payload = response.read()
    assert response.code == expected, f"{path}: expected {expected}, got {response.code}"
    checks += 1
    parsed = json.loads(payload) if payload else None
    return (parsed, response) if return_response else parsed


def login(username, password):
    data = request("/api/auth/login", {"username": username, "password": password}, auth=False)
    assert data["success"] and data["data"]["token"], "login did not return a token"
    return data["data"]["token"]


def token_claims(value):
    payload = value.split(".")[1]
    payload += "=" * (-len(payload) % 4)
    return json.loads(base64.urlsafe_b64decode(payload))


def balance(account_id):
    return Decimal(str(request(f"/api/accounts/{account_id}")["data"]["balance"]))


# --- Baseline scenarios -----------------------------------------------------
assert request("/health", auth=False)["status"] == "ok"
request("/api/accounts", expected=401, auth=False)
request("/api/auth/login", {"username": "admin", "password": "incorrect-demo-password"}, expected=400, auth=False)

token = login("admin", credentials["password"])
admin_claims = token_claims(token)
assert uuid.UUID(admin_claims["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"])
assert admin_claims["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] == "Admin"

request("/swagger/v1/swagger.json", auth=False)
customer = request("/api/customers", {
    "firstName": "Synthetic",
    "lastName": "SmokeTest",
    "identityNumber": "DEMO-" + uuid.uuid4().hex[:12],
}, expected=201)["data"]
customers = request("/api/customers")["data"]
assert any(item["id"] == customer["id"] for item in customers)

accounts = []
for _ in range(2):
    account = request("/api/accounts/open", {"customerId": customer["id"], "currency": "TRY"})["data"]
    accounts.append(account["id"])
first, second = accounts
assert request("/api/accounts/open", {"customerId": customer["id"], "currency": "BTC"}, expected=400)

request("/api/transactions/deposit", {"accountId": first, "amount": 100.25})
request("/api/transactions/withdraw", {"accountId": first, "amount": 10.10})
request("/api/accounts/transfer", {"fromAccountId": first, "toAccountId": second, "amount": 20.05})
request("/api/transactions/withdraw", {"accountId": first, "amount": 100000}, expected=400)
request("/api/transactions/deposit", {"accountId": first, "amount": -1}, expected=400)
assert balance(first) == Decimal("70.10")
assert balance(second) == Decimal("20.05")
request(f"/api/transactions/by-account/{first}")
now = datetime.now(timezone.utc)
dates = urllib.parse.urlencode({"from": (now - timedelta(days=1)).isoformat(), "to": (now + timedelta(days=1)).isoformat()})
request(f"/api/transactions/daily-summary/{first}?{dates}")
request(f"/api/transactions/top-accounts?{dates}&take=5&currency=TRY")

# --- Role and authorization checks -----------------------------------------
user_token = login("tamer", credentials["password"])
user_claims = token_claims(user_token)
assert user_claims["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] == "User"
request("/api/customers", token_override=user_token, expected=403)
request("/api/accounts", token_override=user_token, expected=403)
request(f"/api/transactions/by-account/{first}", token_override=user_token, expected=403)
request(f"/api/transactions/top-accounts?{dates}&take=5&currency=TRY", token_override=user_token, expected=403)
request("/api/transactions/deposit", {"accountId": first, "amount": 1}, token_override=user_token, expected=403)
request("/api/transactions/withdraw", {"accountId": first, "amount": 1}, token_override=user_token, expected=403)
request("/api/accounts/transfer", {"fromAccountId": first, "toAccountId": second, "amount": 1},
        token_override=user_token, expected=403)
request("/api/paymentapprovals/pending", token_override=user_token, expected=403)
request("/api/audit", token_override=user_token, expected=403)
request("/api/users", token_override=user_token, expected=403)

# --- Second Admin + maker-checker ------------------------------------------
checker_name = "smoke-checker-" + uuid.uuid4().hex[:6]
checker_password = "SyntheticCheck1"
request("/api/users", {"username": checker_name, "password": checker_password, "role": "Admin"})
users = request("/api/users")["data"]
assert any(item["username"] == checker_name for item in users)
checker_token = login(checker_name, checker_password)

request("/api/transactions/deposit", {"accountId": first, "amount": 100000},
        headers={"Idempotency-Key": "smoke-dep-" + uuid.uuid4().hex})
assert balance(first) == Decimal("100070.10")

approval = request("/api/paymentapprovals", {
    "fromAccountId": first,
    "toAccountId": second,
    "amount": 75000,
    "description": "Synthetic maker-checker smoke",
}, expected=201)["data"]
approval_id = approval["id"]
assert approval["status"] == "Pending" and approval["riskLevel"] == "Medium"
assert approval["currency"] == "TRY"
assert uuid.UUID(approval["requestedByUserId"])
pending = request("/api/paymentapprovals/pending")["data"]
assert any(item["id"] == approval_id for item in pending)

request(f"/api/paymentapprovals/{approval_id}/approve", method="POST", expected=400)
request(f"/api/paymentapprovals/{approval_id}/approve", method="POST", token_override=checker_token, expected=204)
assert balance(first) == Decimal("25070.10")
assert balance(second) == Decimal("75020.05")
request(f"/api/paymentapprovals/{approval_id}/approve", method="POST", token_override=checker_token, expected=400)
assert balance(first) == Decimal("25070.10")
assert balance(second) == Decimal("75020.05")

low = request("/api/paymentapprovals", {
    "fromAccountId": first,
    "toAccountId": second,
    "amount": 100,
    "description": "Synthetic low risk smoke",
}, expected=201)["data"]
assert low["status"] == "AutoApproved" and low["riskLevel"] == "Low"

request("/api/transactions/deposit", {"accountId": first, "amount": 100000},
        headers={"Idempotency-Key": "smoke-dep-" + uuid.uuid4().hex})
before_reject_first = balance(first)
before_reject_second = balance(second)
rejected = request("/api/paymentapprovals", {
    "fromAccountId": first,
    "toAccountId": second,
    "amount": 50000,
    "description": "Synthetic reject smoke",
}, expected=201)["data"]
request(f"/api/paymentapprovals/{rejected['id']}/reject",
        {"rejectionReason": "Synthetic smoke rejection"},
        token_override=checker_token, expected=204)
assert balance(first) == before_reject_first
assert balance(second) == before_reject_second

# --- Idempotency ------------------------------------------------------------
idem_key = "smoke-idem-" + uuid.uuid4().hex
before_idem_first = balance(first)
_, first_meta = request("/api/transactions/deposit", {"accountId": first, "amount": 10.10},
                        headers={"Idempotency-Key": idem_key}, return_response=True)
_, replay_meta = request("/api/transactions/deposit", {"accountId": first, "amount": 10.10},
                         headers={"Idempotency-Key": idem_key}, return_response=True)
assert replay_meta.headers.get("Idempotency-Replayed") == "true"
assert balance(first) == before_idem_first + Decimal("10.10")
request("/api/transactions/deposit", {"accountId": first, "amount": 99},
        headers={"Idempotency-Key": idem_key}, expected=409)

# --- Currency correctness ---------------------------------------------------
usd_account = request("/api/accounts/open", {
    "customerId": customer["id"],
    "currency": "USD",
    "initialBalance": 10.05,
})["data"]
assert usd_account["currency"] == "USD" and Decimal(str(usd_account["balance"])) == Decimal("10.05")
request("/api/accounts/transfer", {"fromAccountId": first, "toAccountId": usd_account["id"], "amount": 1},
        expected=400)
request("/api/paymentapprovals", {"fromAccountId": first, "toAccountId": usd_account["id"], "amount": 1},
        expected=400)

# --- Audit ------------------------------------------------------------------
audit = request("/api/audit?pageSize=100")["data"]
actions = {item["action"] for item in audit["items"]}
assert {"PAYMENT_APPROVED", "PAYMENT_REJECTED", "PAYMENT_AUTO_APPROVED", "DEPOSIT_SUCCESS"} <= actions
audit_text = json.dumps(audit).lower()
assert credentials["password"].lower() not in audit_text
assert "bearer " not in audit_text

# --- Invalid token and login throttle --------------------------------------
request("/api/accounts", token_override="not.a.real.token", expected=401)
lock_name = "lockout-" + uuid.uuid4().hex[:8]
for _ in range(5):
    request("/api/auth/login", {"username": lock_name, "password": "WrongPass1"}, expected=400, auth=False)
request("/api/auth/login", {"username": lock_name, "password": "WrongPass1"}, expected=429, auth=False)

# --- Dashboard session, role visibility and logout -------------------------
class AntiForgeryParser(HTMLParser):
    value = None

    def handle_starttag(self, tag, attrs):
        data = dict(attrs)
        if tag == "input" and data.get("name") == "__RequestVerificationToken":
            self.value = data.get("value")


def dashboard_login(browser, username, password):
    page = browser.open(dashboard + "/Login", timeout=20)
    form = AntiForgeryParser()
    form.feed(page.read().decode())
    assert form.value, "Dashboard login antiforgery token missing"
    payload = urllib.parse.urlencode({
        "Username": username,
        "Password": password,
        "__RequestVerificationToken": form.value,
    }).encode()
    page = browser.open(urllib.request.Request(dashboard + "/Login", data=payload), timeout=20)
    assert urllib.parse.urlparse(page.url).path == "/", "Dashboard login did not establish a session"
    checks_increment()


def checks_increment():
    global checks
    checks += 1


browser = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
dashboard_login(browser, "admin", credentials["password"])
dashboard_dates = urllib.parse.urlencode({
    "from": (now.date() - timedelta(days=1)).isoformat(),
    "to": (now.date() + timedelta(days=1)).isoformat(),
    "currency": "TRY",
    "limit": 5,
})
users_page = browser.open(dashboard + "/Admin/Users", timeout=20).read().decode()
assert "Kullanıcılar" in users_page and "alert-danger" not in users_page
report_page = browser.open(dashboard + "/Reports/TopAccounts?" + dashboard_dates, timeout=20).read().decode()
assert "alert-danger" not in report_page
home_page = browser.open(dashboard + "/", timeout=20).read().decode()
assert "admin" in home_page
browser.open(dashboard + "/Logout", timeout=20)
after_logout = browser.open(dashboard + "/", timeout=20)
assert urllib.parse.urlparse(after_logout.url).path == "/Login"

dashboard_login(browser, "tamer", credentials["password"])
denied = browser.open(dashboard + "/Admin/Users", timeout=20)
denied_html = denied.read().decode()
assert urllib.parse.urlparse(denied.url).path == "/" and "Admin yetkisi" in denied_html
denied_report = browser.open(dashboard + "/Reports/TopAccounts?" + dashboard_dates, timeout=20)
denied_report_html = denied_report.read().decode()
assert urllib.parse.urlparse(denied_report.url).path == "/" and "Admin yetkisi" in denied_report_html
browser.open(dashboard + "/Logout", timeout=20)
anonymous = browser.open(dashboard + "/Reports/TopAccounts?" + dashboard_dates, timeout=20)
assert urllib.parse.urlparse(anonymous.url).path == "/Login"

print(f"PASS: {checks} local checks: health, authorization, claims/roles, login throttle, "
      "customers, accounts, decimal balances, currency, transfer, maker-checker, "
      "double approval rejection, idempotent replay, audit, reports and dashboard session/role flows.")
