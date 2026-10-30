import {readFileSync,existsSync} from 'node:fs';
import {createRequire} from 'node:module';
import {dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {runInNewContext} from 'node:vm';
import ts from 'typescript';

// Execute local components, including their real TSX dependencies, without a Next build.
export function loadTsx(file,overrides={}){
 const filename=file instanceof URL?fileURLToPath(file):file;
 const nativeRequire=createRequire(filename);
 const exports={};
 runInNewContext(ts.transpileModule(readFileSync(filename,'utf8'),{
  compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022,jsx:ts.JsxEmit.ReactJSX}
 }).outputText,{
  exports,...overrides.globals,
  require(name){
   if(Object.hasOwn(overrides.modules??{},name))return overrides.modules[name];
   if(name.startsWith('.')){
    const path=resolve(dirname(filename),name);
    const source=['.tsx','.ts'].map(ext=>path+ext).find(existsSync);
    if(source)return loadTsx(source,overrides);
   }
   return nativeRequire(name);
  }
 });
 return exports;
}
