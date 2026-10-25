const fs=require('fs'),p=require('path'),{spawnSync}=require('child_process');
const root=p.resolve('Exports/ActionTrailer'),w=p.join(root,'Edit'),vdir=p.join(root,'Voice');fs.mkdirSync(w,{recursive:true});fs.mkdirSync(vdir,{recursive:true});
function ff(a){const r=spawnSync('ffmpeg',['-hide_banner','-loglevel','error','-y',...a],{stdio:'inherit'});if(r.status!==0)throw Error('ffmpeg '+r.status);}
const contrast=process.argv.includes('--contrast');
const selectedRefresh=process.argv.find(a=>a.startsWith('--refresh='))?.slice(10).split(',');
const princess=contrast||process.argv.includes('--princess');
const old=n=>princess&&n==='04-palace'?p.join(root,'NewModel','palace.mp4'):p.resolve('Exports/Trailer/Edit/'+n+'.mp4'),ed=n=>princess&&[4,5,9].includes(n)?p.join(root,'NewModel','ED'+n+'.mp4'):p.resolve('Exports/Endings/ED'+n+'.mp4'),ui=n=>p.join(root,'UI',n+'.png');
const refresh=princess?['04-palace','08-pause','09-rewind','14-archive','15-pursuit','17-escape','18-banquet','impact-1','impact-2','impact-3','impact-5','impact-6']:['08-pause','15-pursuit','impact-3'];
const parts=[],timeline=[];let time=0;
function shot(name,input,start,d,{overlay,zoom=1.03,push=.04,shake=0,speed=1,filter='',card=false,flash=false}={}){
 const out=p.join(w,name+'.mp4');
 if(process.argv.includes('--reuse')&&(selectedRefresh?!selectedRefresh.includes(name):!contrast&&!refresh.includes(name))&&fs.existsSync(out)){parts.push(out);timeline.push({name,start:time,duration:d});time+=d;return;}
 let a=['-ss',String(start),'-i',input];if(overlay)a.push('-loop','1','-i',ui(overlay));
 const z=`${zoom}+${push}*on/${Math.max(1,Math.round(d*30))}`;
 const gentle=contrast&&/^(0[1-7]|10)-/.test(name);
 let vf=`trim=duration=${d*speed},setpts=(PTS-STARTPTS)/${speed},fps=30,scale=1920:1080,zoompan=z='${z}':x='iw/2-iw/zoom/2+${shake}*sin(on*2.1)':y='ih/2-ih/zoom/2+${shake*.6}*cos(on*1.7)':d=1:s=1920x1080:fps=30,`+(gentle?'eq=contrast=1.01:saturation=1.06:brightness=0.012':'eq=contrast=1.08:saturation=0.86:brightness=-0.012')+',unsharp=5:5:0.35'+(gentle?'':',crop=1920:804:0:138,pad=1920:1080:0:138:black');
 if(filter)vf+=','+filter;
 if(flash)vf+=",eq=brightness='if(lt(t,0.067),0.14,0)':eval=frame";
 if(card)vf+=`,fade=t=in:d=0.08,fade=t=out:st=${d-.08}:d=0.08`;
 if(overlay)a.push('-filter_complex',`[0:v]${vf}[base];[base][1:v]overlay=0:0:shortest=1[out]`,'-map','[out]');else a.push('-vf',vf,'-map','0:v');
 a.push('-t',String(d),'-an','-c:v','libx264','-preset','fast','-crf','18','-pix_fmt','yuv420p',out);ff(a);parts.push(out);timeline.push({name,start:time,duration:d});time+=d;console.log(name,time.toFixed(2));
}
function concat(files,out){const list=out+'.txt';fs.writeFileSync(list,files.map(f=>"file '"+f.replaceAll('\\','/')+"'").join('\n'));ff(['-f','concat','-safe','0','-i',list,'-c','copy',out]);}
async function voices(){const sp=await(await fetch('http://127.0.0.1:50021/speakers')).json();const id=sp.find(s=>s.name==='栗田まろん').styles.find(s=>s.name==='ノーマル').id;
const lines=[['intro','もし、絵本の世界に入れたら。',.95],['question','本当に、カメを助けたのでしょうか？',.89],['choice','その選択が、運命を変える。',.97],['nine','知っている物語に、知らない結末を。',1.04],['final','物語を、選び直せ。',.88]];
for(const[n,text,speed]of lines){let q=await(await fetch('http://127.0.0.1:50021/audio_query?'+new URLSearchParams({text,speaker:id}),{method:'POST'})).json();Object.assign(q,{speedScale:speed,pitchScale:-.025,intonationScale:.9,prePhonemeLength:.08,postPhonemeLength:.15,outputSamplingRate:48000});const r=await fetch('http://127.0.0.1:50021/synthesis?speaker='+id,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(q)});if(!r.ok)throw Error(await r.text());fs.writeFileSync(p.join(vdir,n+'.wav'),Buffer.from(await r.arrayBuffer()));}
fs.writeFileSync(p.join(vdir,'credits.json'),JSON.stringify({voice:'VOICEVOX：栗田まろん',lines},null,2));}
async function main(){if(!process.argv.includes('--reuse')||!['intro','question','choice','nine','final'].every(n=>fs.existsSync(p.join(vdir,n+'.wav'))))await voices();
 const black=p.join(w,'black.mp4');ff(['-f','lavfi','-i','color=c=0x05080d:s=1920x1080:r=30:d=8','-c:v','libx264','-pix_fmt','yuv420p',black]);
 if(contrast){
 shot('01-room',old('01-room'),.3,4.5,{zoom:1,push:.018});
 shot('02-beach',old('02-beach'),.35,2.8,{zoom:1,push:.018});
 shot('03-sea',old('03-sea'),.35,2.8,{zoom:1,push:.025});
 shot('04-palace',old('04-palace'),.2,2.8,{zoom:1,push:.018});
 shot('05-village',old('05-village'),.2,1.6,{zoom:1,push:.012});
 shot('06-smoke',ed(6),.2,2.5,{zoom:1,push:.015});
 }else{
 shot('01-room',old('01-room'),.3,4.5,{zoom:1.01,push:.1});
 shot('02-beach',old('02-beach'),.3,1.4,{push:.12});shot('03-sea',old('03-sea'),.3,1.4,{zoom:1.06,push:.18});shot('04-palace',old('04-palace'),.2,1.4,{zoom:1.02,push:.15});shot('05-village',old('05-village'),.3,.8,{zoom:1.1,push:.08});shot('06-smoke',ed(6),.2,1.5,{zoom:1.08,push:.18});
 }
 shot('07-happy',black,0,1.5,{overlay:'happy',card:true});shot('08-pause',p.join(w,'07-happy.mp4'),.2,1,{overlay:'pause',zoom:1,push:0});
 const prev=p.join(w,'preceding.mp4');concat(parts.slice(1,6),prev);
 // Reverse actual preceding scenes at speed; end on the beach.
 const rev=p.join(w,'reverse.mp4');const rewindRate=timeline.slice(1,6).reduce((s,x)=>s+x.duration,0)/2;ff(['-i',prev,'-vf',`reverse,setpts=PTS/${rewindRate},fps=30`,'-an','-c:v','libx264','-preset','fast','-crf','18',rev]);shot('09-rewind',rev,0,2,{overlay:'rewind',zoom:1,push:0});
 shot('10-bully',old('10-bully'),.15,3,{zoom:1.03,push:contrast?.025:.13,shake:contrast?0:5,speed:1.4});shot('11-question',contrast?ed(2):black,contrast?12:0,4,{overlay:'question',card:true,zoom:contrast?1.18:1.03,push:contrast?.25:.04,filter:contrast?'eq=brightness=-0.23:saturation=0.4,drawbox=x=0:y=850:w=iw:h=230:color=black:t=fill':''});shot('12-choice',black,0,2,{overlay:'choice',card:true});
 shot('13-turtle',ed(2),11.7,1.7,{overlay:'ed2',zoom:1.08,push:.22,shake:5});shot('14-archive',ed(4),30,1.6,{overlay:'ed4',zoom:1.05,push:.16,shake:3});shot('15-pursuit',contrast?ed(5):ed(9),contrast?20.5:princess?30:24.5,1.2,{overlay:contrast?'ed5':'ed9',zoom:1.08,push:.14,shake:7});shot('16-run',black,0,.6,{overlay:'run',card:true});shot('17-escape',ed(9),princess?44.5:43,1.3,{overlay:'ed9',zoom:1.1,push:.1,shake:9,flash:true});
 shot('18-banquet',ed(5),princess?24.2:22,1.8,{overlay:'ed5',zoom:1.07,push:.17,shake:3});shot('19-boat',ed(3),6,1.6,{overlay:'ed3',zoom:1.08,push:.1});shot('20-lost',ed(1),8,1.6,{overlay:'ed1',zoom:1.12,push:.12});shot('21-nine',black,0,1.4,{overlay:'nine',card:true});shot('22-new-life',ed(7),7,1.7,{overlay:'ed7',zoom:1.03,push:.08});shot('23-promise',ed(8),48,2.3,{overlay:'ed8',zoom:1.06,push:.12});shot('24-box',ed(6),1,1.6,{overlay:'ed6',zoom:1.08,push:.16,shake:4});
 const rapid=[[ed(2),13],[ed(4),princess?30.8:32],[ed(5),contrast?20.6:princess?24.8:24],[contrast?old('03-sea'):ed(9),contrast?1.7:princess?30.5:25.3],[old('10-bully'),1.35],[ed(9),princess?45.3:44],[contrast?ed(8):ed(4),contrast?48.5:princess?31.5:34],[ed(2),15],[ed(6),.3],[old('01-room'),4.8]];
 rapid.forEach(([f,t],i)=>shot('impact-'+i,f,t,.45,{zoom:1.12,push:.2,shake:8,flash:i===0||i===5}));
 shot('25-silence',black,0,.4,{push:0});shot('26-title',black,0,5.5,{overlay:'title',card:true});shot('27-sting',ed(2),14.6,1,{zoom:1.2,push:.34,shake:8});shot('28-end',black,0,1,{push:0});
 const picture=p.join(w,'picture.mp4');concat(parts,picture);fs.writeFileSync(p.join(root,'timeline.json'),JSON.stringify(timeline,null,2));
 score();mux(picture);
 function score(){const rate=48000,buf=Buffer.alloc(Math.ceil(time*rate)*2+44);buf.write('RIFF');buf.writeUInt32LE(buf.length-8,4);buf.write('WAVEfmt ',8);buf.writeUInt32LE(16,16);buf.writeUInt16LE(1,20);buf.writeUInt16LE(1,22);buf.writeUInt32LE(rate,24);buf.writeUInt32LE(rate*2,28);buf.writeUInt16LE(2,32);buf.writeUInt16LE(16,34);buf.write('data',36);buf.writeUInt32LE(buf.length-44,40);
 const shift=timeline.find(x=>x.name==='11-question').start-18.5;
 const hits=timeline.filter(x=>x.start>=24.5+shift&&x.start<47.4+shift||(!contrast&&x.name==='10-bully')||x.name==='27-sting'||(contrast&&x.name==='11-question')).map(x=>x.start);let seed=12345;const noise=()=>{seed=(Math.imul(seed,1664525)+1013904223)|0;return(seed>>>0)/2147483648-1};
 for(let i=0;i<(buf.length-44)/2;i++){const realT=i/rate,t=realT-shift;let s=0,n=noise();
 if(contrast){
 const happyEnd=timeline.find(x=>x.name==='08-pause').start;
 if(realT<happyEnd||(t>=15.5&&t<18.5)){
 let u=realT%.78,k=Math.floor(realT/.78),f=[261.63,329.63,392,523.25,440,392,329.63,293.66][k%8];
 const env=Math.min(1,realT/1.2,realT<happyEnd?(happyEnd-realT)/.8:(18.5-t)/.45);
 s=(.085*(Math.sin(2*Math.PI*f*realT)+.23*Math.sin(2*Math.PI*f*2*realT))*Math.exp(-u*4.5)+.018*(Math.sin(2*Math.PI*130.81*realT)+.6*Math.sin(2*Math.PI*164.81*realT)+.4*Math.sin(2*Math.PI*196*realT)))*Math.max(0,env);
 }
 if(t>=18.5&&t<22.5){let u=t-18.5;s+=.018*Math.sin(2*Math.PI*43*u)*Math.exp(-u*.7);}
 }else if(t<11){let k=Math.floor(t/.45),u=t%.45,f=[220,261.63,329.63,440,392,329.63,261.63,293.66][k%8];s=.065*Math.sin(2*Math.PI*f*t)*Math.exp(-u*8)+.022*Math.sin(2*Math.PI*110*t);if(t>4.5){let b=(t-4.5)%.7;s+=.18*Math.sin(2*Math.PI*(55*b+8*(1-Math.exp(-b*20))))*Math.exp(-b*12);}}
 if(t>=13.5&&t<15.5){let u=t-13.5;s=.07*Math.sin(2*Math.PI*(400*u+600*u*u))+.025*n;}
 if(!contrast&&t>=15.5&&t<18.5){let u=(t-15.5)%.75;s=.22*Math.sin(2*Math.PI*62*u)*Math.exp(-u*20)+.025*n*Math.exp(-u*15);}
 if(t>=22.5&&t<47.4){let beat=(t-22.5)%.375,bar=Math.floor((t-22.5)/3)%4;let bass=[55,65.406,49,58.27][bar];s=.085*Math.sin(2*Math.PI*bass*t)+.035*Math.sin(2*Math.PI*bass*1.5*t);s+=.21*Math.sin(2*Math.PI*(48*beat+3*(1-Math.exp(-beat*35))))*Math.exp(-beat*15);s+=.035*n*Math.exp(-beat*45);let arp=(t-22.5)%.1875,st=Math.floor((t-22.5)/.1875)%4;const f=bass*[4,6,8,6][st];s+=.035*(Math.sin(2*Math.PI*f*t)+.3*Math.sin(2*Math.PI*f*2*t))*Math.exp(-arp*10);if(t>41.3){let r=(t-41.3)/6.1;s+=r*.045*n+r*.04*Math.sin(2*Math.PI*(220*t+70*(t-41.3)**2));}}
 for(const h of hits){let u=realT-h;if(u>=0&&u<.65)s+=.20*Math.sin(2*Math.PI*(40*u+2*(1-Math.exp(-u*30))))*Math.exp(-u*9)+.045*n*Math.exp(-u*17);}
 if(t>=47.8&&t<53.3){let u=t-47.8;s=.045*(Math.sin(2*Math.PI*110*t)+.6*Math.sin(2*Math.PI*130.81*t)+.5*Math.sin(2*Math.PI*164.81*t))*Math.exp(-u*.35);}
 s=Math.tanh(s*1.1)*.8;buf.writeInt16LE(Math.round(s*32767),44+i*2);}
 fs.writeFileSync(p.join(w,'score.wav'),buf);}
 function mux(picture){const shift=timeline.find(x=>x.name==='11-question').start-18.5;const happyAt=timeline.find(x=>x.name==='07-happy').start;const timings=[['intro',.3],['question',18.8],['choice',22.7],['nine',35.8],['final',48.4]].map(([n,t])=>[n,n==='intro'?t:t+shift]);const args=['-i',picture,'-i',p.join(w,'score.wav')];timings.forEach(([n])=>args.push('-i',p.join(vdir,n+'.wav')));args.push('-i',p.resolve('Assets/_Project/Audio/Voice/Resources/Narration/medetashi.wav'));let af='[1:a]volume=0.9[music];';timings.forEach(([n,t],i)=>af+=`[${i+2}:a]volume=1.4,adelay=${Math.round(t*1000)}:all=1[v${i}];`);af+='[7:a]volume=1.3,adelay='+Math.round(happyAt*1000)+':all=1[happy];[music][v0][v1][v2][v3][v4][happy]amix=inputs=7:duration=longest:normalize=0,apad,loudnorm=I=-16:TP=-1.5:LRA=9[a]';args.push('-filter_complex',af,'-map','0:v','-map','[a]','-t',String(time),'-c:v','copy','-c:a','aac','-ar','48000','-ac','2','-b:a','192k','-movflags','+faststart',p.join(root,contrast?'Otogi_Trailer_Contrast.mp4':princess?'Otogi_Action_Trailer_Princess.mp4':'Otogi_Action_Trailer.mp4'));ff(args);console.log('COMPLETE',time);}
}
main().catch(e=>{console.error(e);process.exit(1)});







