import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {runInNewContext} from 'node:vm';
import ts from 'typescript';
const exports = {};
runInNewContext(ts.transpileModule(readFileSync(new URL('../lib/otp-delivery-feedback.ts', import.meta.url),'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS}}).outputText,{exports});
test('preserves maintenance and recovery causes without exposing upstream diagnostics',()=>{
 for(const error of ['maintenance_mode','recovery_mode'])assert.equal(exports.otpUnavailableCode({error}),error);
 for(const body of [null,{}, {error:'secret provider diagnostics'}])assert.equal(exports.otpUnavailableCode(body),'otp_unavailable');
});
test('maintenance gives Level1 recovery instructions instead of SMS fault',()=>{
 assert.match(exports.otpUnavailableMessage('maintenance_mode'),/پایان نگهداری/);
 assert.match(exports.otpUnavailableMessage('recovery_mode'),/بررسی بازیابی/);
 assert.match(exports.otpUnavailableMessage('identity_service_unavailable'),/احراز هویت/);
 assert.match(exports.otpUnavailableMessage('otp_unavailable'),/سرویس پیامک/);
});
