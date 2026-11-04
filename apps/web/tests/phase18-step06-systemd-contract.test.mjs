import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const read = name => readFileSync(new URL("../../ops/phase18-systemd/" + name, import.meta.url), "utf8");
const api = read("argintaxpayer-api.service");
const web = read("argintaxpayer-web.service");
const parse = text => {
  let section = "";
  const entries = [];
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith("#")) continue;
    if (/^\[[^\]]+\]$/.test(line)) { section = line.slice(1, -1); continue; }
    const equal = line.indexOf("=");
    assert.ok(equal > 0, "invalid unit line: " + line);
    entries.push({section, key: line.slice(0, equal), value: line.slice(equal + 1)});
  }
  return entries;
};
const one = (unit, section, key) => {
  const v = parse(unit).filter(x => x.section === section && x.key === key);
  assert.equal(v.length, 1, section + "." + key + " must be unique");
  return v[0].value;
};

test("Step06 units are disabled by default and require independent owner gate", () => {
  for (const unit of [api, web]) {
    assert.equal(one(unit, "Unit", "ConditionPathExists"),
      "/etc/argintaxpayer/phase18-activation-approved");
    assert.equal(one(unit, "Install", "WantedBy"), "multi-user.target");
    assert.equal(one(unit, "Service", "User"), "argintaxsvc");
    assert.equal(one(unit, "Service", "Group"), "argintaxsvc");
    assert.equal(one(unit, "Service", "Restart"), "on-failure");
    assert.equal(one(unit, "Unit", "StartLimitBurst"), "3");
    assert.equal(one(unit, "Unit", "StartLimitIntervalSec"), "300");
    assert.equal(one(unit, "Service", "NoNewPrivileges"), "true");
    assert.equal(one(unit, "Service", "ProtectSystem"), "strict");
    assert.equal(one(unit, "Service", "ProtectHome"), "true");
    assert.equal(one(unit, "Service", "PrivateTmp"), "true");
    assert.equal(one(unit, "Service", "CapabilityBoundingSet"), "");
    assert.equal(one(unit, "Service", "AmbientCapabilities"), "");
    assert.equal(one(unit, "Service", "UMask"), "0077");
    const command = one(unit, "Service", "ExecStart");
    assert.doesNotMatch(command, /\b(dev|watch|--inspect|hot-reload)\b/);
    assert.doesNotMatch(unit, /WantedBy=default.target|ExecStartPre=.*migrat/i);
    assert.doesNotMatch(unit, /0\.0\.0\.0/);
    assert.doesNotMatch(unit, /docker|postgres-postgres-1/);
  }
});
test("Dedicated loopback process ports are distinct and Next never binds publicly", () => {
  assert.match(api, /^Environment=ASPNETCORE_ENVIRONMENT=Production$/m);
  assert.match(api, /^Environment=ASPNETCORE_URLS=http:\/\/127\.0\.0\.1:15090$/m);
  assert.match(web, /^ExecStart=.*next start --hostname 127\.0\.0\.1 --port 15091$/m);
  assert.match(web, /^Environment=ARGIN_IDENTITY_API_INTERNAL_BASE_URL=http:\/\/127\.0\.0\.1:15090$/m);
  assert.match(web, /^Environment=ARGIN_PRODUCTION_UI_ENABLED=false$/m);
  assert.match(api, /^Environment=ArginTaxPayer__EnableOperationalEndpoints=false$/m);
  assert.match(api, /^Environment=Ghasedak__EnableLiveSending=false$/m);
  assert.match(api, /^Environment=SandboxInvoiceLegacy__Enabled=false$/m);
  assert.match(web, /^ReadWritePaths=\/opt\/argintaxpayer\/current\/web\/\.next\/cache$/m);
});
test("Step06 template never contains secrets, live fiscal endpoints or automatic migration", () => {
  for (const unit of [api, web]) {
    assert.doesNotMatch(unit, /Password=|ApiKey=|token=|private.?key=|tax\.gov\.ir/i);
    assert.doesNotMatch(unit, /ExecStartPre=.*(psql|dotnet ef|migrate)/i);
  }
});
