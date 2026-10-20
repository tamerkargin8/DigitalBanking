#!/usr/bin/env python3
"""Exercise only the local synthetic playground. Never prints tokens or passwords."""
import argparse
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

def request(path, body=None, expected=200, auth=True):
    global checks
    headers = {"Content-Type": "application/json"}
    if auth and token:
        headers["Authorization"] = "Bearer " + token
    req = urllib.request.Request(api + path, data=json.dumps(body).encode() if body is not None else None, headers=headers)
    try:
        response = urllib.request.urlopen(req, timeout=20)
    except urllib.error.HTTPError as error:
        response = error
    payload = response.read()
    assert response.code == expected, f"{path}: expected {expected}, got {response.code}"
    checks += 1
    return json.loads(payload) if payload else None

assert request("/health", auth=False)["status"] == "ok"
request("/api/accounts", expected=401, auth=False)
request("/api/auth/login", {"username": "admin", "password": "incorrect-demo-password"}, expected=400, auth=False)
login = request("/api/auth/login", {"username": "admin", "password": credentials["password"]}, auth=False)
assert login["success"] and login["data"]["token"]
token = login["data"]["token"]
request("/swagger/v1/swagger.json", auth=False)
customer = request("/api/customers", {"firstName": "Synthetic", "lastName": "SmokeTest", "identityNumber": "DEMO-" + uuid.uuid4().hex[:12]}, expected=201)
request("/api/customers")
accounts = []
for _ in range(2):
    account = request("/api/accounts/open", {"customerId": customer["id"], "currency": "TRY"})["data"]
    accounts.append(account["id"])
first, second = accounts
request("/api/transactions/deposit", {"accountId": first, "amount": 100.25})
request("/api/transactions/withdraw", {"accountId": first, "amount": 10.10})
request("/api/accounts/transfer", {"fromAccountId": first, "toAccountId": second, "amount": 20.05})
request("/api/transactions/withdraw", {"accountId": first, "amount": 100000}, expected=400)
request("/api/transactions/deposit", {"accountId": first, "amount": -1}, expected=400)
assert Decimal(str(request(f"/api/accounts/{first}")["data"]["balance"])) == Decimal("70.10")
assert Decimal(str(request(f"/api/accounts/{second}")["data"]["balance"])) == Decimal("20.05")
request(f"/api/transactions/by-account/{first}")
now = datetime.now(timezone.utc)
dates = urllib.parse.urlencode({"from": (now - timedelta(days=1)).isoformat(), "to": (now + timedelta(days=1)).isoformat()})
request(f"/api/transactions/daily-summary/{first}?{dates}")
request(f"/api/transactions/top-accounts?{dates}&take=5")

class AntiForgeryParser(HTMLParser):
    value = None
    def handle_starttag(self, tag, attrs):
        data = dict(attrs)
        if tag == "input" and data.get("name") == "__RequestVerificationToken":
            self.value = data.get("value")

browser = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
page = browser.open(dashboard + "/Login", timeout=20)
form = AntiForgeryParser()
form.feed(page.read().decode())
assert form.value, "Dashboard login antiforgery token missing"
payload = urllib.parse.urlencode({"Username": "admin", "Password": credentials["password"], "__RequestVerificationToken": form.value}).encode()
page = browser.open(urllib.request.Request(dashboard + "/Login", data=payload), timeout=20)
assert urllib.parse.urlparse(page.url).path == "/", "Dashboard login did not establish a session"
checks += 1
report = browser.open(dashboard + "/Reports/TopAccounts?" + dates + "&limit=5", timeout=20)
html = report.read().decode()
assert report.code == 200 and "alert-danger" not in html, "Dashboard report contains an error"
checks += 1
print(f"PASS: {checks} local checks: health, authorization, login, Swagger, customers, accounts, decimal balances, transfer, rejection paths, reports and dashboard session.")
