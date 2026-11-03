import test from 'node:test';
import assert from 'node:assert/strict';
import {loadTsx} from './helpers/load-tsx.mjs';

const moduleUrl=new URL('../app/(console)/dashboard/dashboard-calendar.tsx',import.meta.url);

test('today includes weekday and full Persian, Gregorian and lunar dates in Tehran',()=>{
 const {calendarDatesFor}=loadTsx(moduleUrl);
 const dates=calendarDatesFor(new Date('2026-11-03T10:00:00Z'));
 assert.equal(dates[0].text,'سه‌شنبه، ۱۲ آبان ۱۴۰۵ (۱۴۰۵/۰۸/۱۲)');
 assert.equal(dates[1].text,'Tuesday, 3 November 2026');
 assert.equal(dates[2].text,'سه‌شنبه، ۲۳ جمادی‌الاول ۱۴۴۸');
 assert.deepEqual(Array.from(dates,date=>date.label),['شمسی','میلادی','قمری']);
});

test('all calendars advance together at Tehran midnight rather than UTC midnight',()=>{
 const {calendarDatesFor}=loadTsx(moduleUrl);
 const before=calendarDatesFor(new Date('2026-11-03T20:29:59Z'));
 const after=calendarDatesFor(new Date('2026-11-03T20:30:00Z'));
 assert.equal(before[0].text,'سه‌شنبه، ۱۲ آبان ۱۴۰۵ (۱۴۰۵/۰۸/۱۲)');
 assert.equal(after[0].text,'چهارشنبه، ۱۳ آبان ۱۴۰۵ (۱۴۰۵/۰۸/۱۳)');
 assert.equal(after[1].text,'Wednesday, 4 November 2026');
 assert.equal(after[2].text,'چهارشنبه، ۲۴ جمادی‌الاول ۱۴۴۸');
});
