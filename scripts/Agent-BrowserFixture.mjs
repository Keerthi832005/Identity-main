// Isolated UI acceptance only. No IAM/PTS database or credentials are used.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const iam = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const iamWeb = path.join(iam, 'src/Frontend/dist/IdentityAdministration.Web/browser');
const ptsWeb = path.join(iam, '../PTS/src/Frontend/Terminal/dist/ProductionTracking.Terminal/browser');
const port = Number(process.env.IAM_AGENT_BROWSER_PORT || 4387);
const origin = `http://127.0.0.1:${port}`;
const machine = { installationId: '11111111-1111-4111-8111-111111111111', deviceId: 101, hostname: 'QA-WORKSTATION', agentVersion: '1.0.0', createdAt: new Date().toISOString(), lastReportAt: new Date().toISOString(), isActive: true, isTrusted: true, trustedUntil: new Date(Date.now()+86400000).toISOString() };
const details = { machine, inventory: { capturedAt: new Date().toISOString(), hostname: machine.hostname, os: 'Microsoft Windows 11 Enterprise', architecture: 'X64', agentVersion: '1.0.0', hardware: { manufacturer: 'Example Manufacturer', model: 'Test workstation', serialNumber: 'SYNTHETIC-001', cpu: 'Example 8-core processor', logicalProcessors: 16, memoryBytes: 34359738368, disks: [{name:'C:',totalBytes:512*1073741824,freeBytes:320*1073741824}] }, software: [{name:'Example ERP Client',version:'4.2',publisher:'Example Software'},{name:'Microsoft Edge',version:'Test version',publisher:'Microsoft Corporation'},{name:'IAM.Agent',version:'1.0.0',publisher:'Internal IT'}], collectionWarnings:['Synthetic browser acceptance data — not a live machine report.'] } };
function json(res, value, status=200) { res.writeHead(status, {'Content-Type':'application/json','Cache-Control':'no-store'});res.end(JSON.stringify(value)); }
http.createServer((req,res)=>{
 const url = new URL(req.url, origin);
 if(url.pathname.includes('/api/v1/auth/browser/')) {
   const claims = { sub:'42', employee_code:'TEST-ADMIN', display_name:'Test administrator', capability:['iam.admin','pts.shopfloor.operate','pts.production.read'], exp:Math.floor(Date.now()/1000)+3600, authorization_version:'1' };
   return json(res,{succeeded:true, accessToken:'test.'+Buffer.from(JSON.stringify(claims)).toString('base64url')+'.test', accessTokenExpiresAt:new Date(Date.now()+3600000).toISOString(),authorizationVersion:1});
 }
 if(url.pathname==='/identity/api/v1/admin/agents') return json(res,{items:[machine],total:1,skip:0,take:50});
 if(url.pathname==='/identity/api/v1/admin/agents/'+machine.installationId) return json(res,details);
 if(url.pathname.includes('/api/')) return json(res,{message:'Isolated browser fixture: endpoint is not implemented.'},404);
 const terminal=url.pathname.startsWith('/terminal/');
 const root=terminal?ptsWeb:iamWeb;
 let relative=terminal?url.pathname.slice('/terminal/'.length):url.pathname.slice(1);
 if(relative==='config.json') return json(res,terminal?{identityBaseUrl:'/identity',identityClientId:'pts-web',defaultEnvironment:'DS4',terminalId:null,allowPreview:false,environments:[{code:'DS4',label:'Development',apiBaseUrl:'/ds4'}]}:{identityBaseUrl:'/identity',identityClientId:'identity-admin-web',applicationName:'IAM — TEST FIXTURE'});
 if(!path.extname(relative)) relative='index.html';
 const file=path.resolve(root,relative);
 if(!file.startsWith(root+path.sep) || !fs.existsSync(file)) {res.writeHead(404);return res.end();}
 const types={'.html':'text/html','.js':'text/javascript','.css':'text/css','.png':'image/png','.svg':'image/svg+xml','.woff2':'font/woff2'};
 res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream'});fs.createReadStream(file).pipe(res);
}).listen(port,'127.0.0.1',()=>process.stdout.write(`IAM.Agent isolated browser fixture: ${origin}/agents and ${origin}/terminal/connect\n`));
