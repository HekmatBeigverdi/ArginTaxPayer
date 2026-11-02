import test from 'node:test';
import assert from 'node:assert/strict';
import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {loadTsx} from './helpers/load-tsx.mjs';
const Pagination=loadTsx(new URL('../app/subscriptions/finance-pagination.tsx',import.meta.url)).default;
test('ten-row pagination shows correct last page and disables next there',()=>{
 const html=renderToStaticMarkup(React.createElement(Pagination,{page:3,totalCount:23,loading:false,onPageChange:()=>{}}));
 assert.ok(html.includes('صفحهٔ ۳ از ۳'));
 assert.match(html,/<button type="button" disabled="">صفحه بعد<\/button>/);
 assert.doesNotMatch(html,/<button type="button" disabled="">صفحه قبل<\/button>/);
});
test('pagination requests the selected server page and disables both actions while loading',()=>{
 const selected=[];
 const tree=Pagination({page:2,totalCount:23,loading:false,onPageChange:p=>selected.push(p)});
 const buttons=tree.props.children.filter(node=>node.type==='button');
 buttons[0].props.onClick();buttons[1].props.onClick();
 assert.deepEqual(selected,[1,3]);
 const html=renderToStaticMarkup(React.createElement(Pagination,{page:2,totalCount:23,loading:true,onPageChange:()=>{}}));
 assert.equal((html.match(/disabled=""/g)??[]).length,2);
});
test('receipt history renders its table and spinner before any response',()=>{
 const History=loadTsx(new URL('../app/subscriptions/collection-history-table.tsx',import.meta.url)).default;
 const html=renderToStaticMarkup(React.createElement(History,{orderId:'order',request:async()=>({items:[],totalCount:0})}));
 assert.ok(html.includes('<table'));
 assert.ok(html.includes('مرجع رسید'));
 assert.match(html,/<tbody><tr><td colSpan="4"/);
 assert.ok(html.includes('در حال دریافت تاریخچه'));
});
