const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const source=fs.readFileSync(require('node:path').join(__dirname,'../assets/mobile/app.js'),'utf8');
const boot=source.slice(source.lastIndexOf('try{accept(SilkNative.state());'));
assert.ok(boot.startsWith('try{accept('));
for(const scenario of ['valid','invalid','bridge-error']){
 let acknowledged=false,failure='',scheduled=false;
 const context={S:null,P:null,timerHandle:null,updateTimer(){},setInterval(){scheduled=true;return 1;},E(){return {};},$(){return {append(){}};},SilkNative:{state(){if(scenario==='bridge-error')throw Error('Bridge unavailable');return '{}';},ready(){assert.ok(context.S&&context.P);acknowledged=true;},startupError(error){failure=error;}}};
 context.accept=()=>{if(scenario==='valid'){context.S={quests:{}};context.P={language:'ru'};}};
 vm.runInNewContext(boot,context);
 assert.equal(acknowledged,scenario==='valid');assert.equal(scheduled,scenario==='valid');assert.equal(Boolean(failure),scenario!=='valid');
}
console.log('3 startup handshake checks passed');
