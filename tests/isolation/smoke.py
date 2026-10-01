"""Run after docker compose up --build. Uses only the local showcase."""
import json
import urllib.request
import urllib.error

BASE = 'http://localhost:5095'
def request(path, body=None, token=None):
    headers = {'Content-Type': 'application/json'}
    if token: headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(BASE + path, data=json.dumps(body).encode() if body else None, headers=headers)
    try:
        with urllib.request.urlopen(req) as res:
            data = res.read()
            return res.status, json.loads(data) if data else None, res.headers
    except urllib.error.HTTPError as e:
        return e.code, None, e.headers

assert request('/health')[0] == 200
assert request('/api/auth/google-config')[1]['enabled'] is False
assert request('/api/auth/google', {'credential': 'demo'})[0] == 503
status, auth, headers = request('/api/auth/login', {'email':'customer@example.test','password':'DemoCustomer123!'})
assert status == 200
assert auth['user']['email'] == 'customer@example.test'
assert "connect-src 'self'" in headers['Content-Security-Policy']
token = auth['token']
assert request('/api/dogs')[0] == 401
assert request('/api/users/customers', token=token)[0] == 403
status, dogs, _ = request('/api/dogs', token=token)
assert status == 200 and len(dogs) == 1 and dogs[0]['name'] == 'Demo Mochi'
for role, password in [('owner','DemoOwner123!'), ('assistant','DemoAssistant123!')]:
    status, auth, _ = request('/api/auth/login', {'email':role+'@example.test','password':password})
    assert status == 200 and auth['user']['role'].lower() == role
print('PASS: local health, demo accounts, fictional dog, access controls, Google disabled, and same-origin CSP.')
