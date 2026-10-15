import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const read=p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");
test("profile is independent from buyers and taxpayers and gate requires completed profile",()=>{
 const gate=read("app/components/console-shell.tsx");
 const profile=read("app/account/software-profile-form.tsx");
 const login=read("../../src/ArginTaxPayer.Infrastructure/Persistence/Identity/SoftwareUserProfile.cs");
 assert.match(gate,/\/api\/software-profile\/me/);
 assert.match(gate,/router\.replace\("\/profile"\)/);
 assert.match(profile,/firstName/);assert.match(profile,/lastName/);
 assert.match(profile,/companyName/);assert.match(profile,/address/);
 assert.match(profile,/upload\(file\)/);
 assert.match(login,/class SoftwareUserProfile/);
});
test("security settings contain MFA but no sessions on profile page",()=>{
 const security=read("app/account/account-controls.tsx");
 assert.match(security,/mfa\/status/);
 assert.doesNotMatch(security,/\/api\/identity\/sessions/);
 assert.doesNotMatch(security,/sessionIds/);
});
test("administrator sees profiles by name, incomplete accounts stay only in users screen",()=>{
 const users=read("app/users/users-workspace.tsx");
 const picker=read("app/parties/participant-registration-modal.tsx");
 const directory=read("app/parties/buyer-taxpayer-directory.tsx");
 assert.match(users,/نام شخص \/ شرکت/);
 assert.match(users,/users-contact-truncated/);
 assert.match(picker,/b\.companyName \|\| b\.personName/);
 assert.doesNotMatch(picker,/\{b\.mobile \|\| b\.email/);
 assert.match(directory,/\.filter\(x => x\.isActive && x\.profileComplete &&/);
});

test("profile and security routes are isolated and have compact separate surfaces",()=>{
 const profilePage=read("app/(console)/profile/page.tsx");
 const securityPage=read("app/(console)/account/page.tsx");
 const form=read("app/account/software-profile-form.tsx");
 const shell=read("app/components/console-shell.tsx");
 const css=read("app/globals.css");
 assert.match(profilePage,/SoftwareProfileForm/);
 assert.doesNotMatch(profilePage,/AccountControls/);
 assert.match(securityPage,/AccountControls/);
 assert.doesNotMatch(securityPage,/SoftwareProfileForm/);
 assert.match(shell,/href="\/profile"/);
 assert.match(shell,/href="\/account"/);
 assert.match(form,/textarea rows=\{3\}/);
 assert.match(form,/onDrop=\{drop\}/);
 assert.match(form,/xhr\.upload\.onprogress/);
 assert.match(form,/role="status" aria-live="polite"/);
 assert.match(css,/\.console-logo-dropzone\{border:2px dashed/);
 assert.match(css,/\.console-settings-page/);
});

test("profile completion gate does not re-check or flash on every page navigation",()=>{
 const gate=read("app/components/console-shell.tsx");
 const form=read("app/account/software-profile-form.tsx");
 assert.match(gate,/\[userId, requiresProfile, profileRetry\]/);
 assert.doesNotMatch(gate,/\[pathname, roles\.join/);
 assert.match(gate,/profileState === "checking"/);
 assert.match(gate,/profileState === "error"/);
 assert.match(gate,/profileState === "incomplete" && pathname !== "\/profile"/);
 assert.match(gate,/argin:software-profile-saved/);
 assert.match(form,/window\.dispatchEvent\(new CustomEvent\("argin:software-profile-saved"/);
 assert.doesNotMatch(form,/router\.refresh\(\)/);
});
