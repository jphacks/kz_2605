// Run from repository root, after TrailerCapture and trailer-ui.ps1.
const fs=require('fs'),p=require('path'),{spawnSync}=require('child_process');
const root=p.resolve('Exports/Trailer'),work=p.join(root,'Edit');fs.mkdirSync(work,{recursive:true});
function run(args){let r=spawnSync('ffmpeg',['-hide_banner','-loglevel','error','-y',...args],{stdio:'inherit'});if(r.status!==0)throw Error('ffmpeg failed '+r.status);}
const src=n=>p.join(root,'Source',n+'.mp4'),ui=n=>p.join(root,'UI',n+'.png'),ed=n=>p.resolve('Exports/Endings/ED'+n+'.mp4');
const parts=[];let cursor=0;const timeline=[];
function clip(name,input,start,dur,{overlay,filter='',audio=false,fade=true,still=false}={}){
 const file=p.join(work,name+'.mp4');let a=[];
 if(still)a.push('-loop','1');if(start)a.push('-ss',String(start));a.push('-i',input);if(overlay)a.push('-loop','1','-i',ui(overlay));
 a.push('-f','lavfi','-i','anullsrc=r=48000:cl=stereo');
 let vf='fps=30,scale=1920:1080,setsar=1'+(filter?','+filter:'');
 if(fade)vf+=`,fade=t=in:st=0:d=0.18,fade=t=out:st=${dur-.18}:d=0.18`;
 if(overlay)a.push('-filter_complex',`[0:v]${vf}[base];[base][1:v]overlay=0:0:shortest=1[v]`,'-map','[v]');else a.push('-vf',vf,'-map','0:v:0');
 a.push('-map',audio?'0:a:0':`${overlay?2:1}:a:0`,'-t',String(dur),'-c:v','libx264','-preset','fast','-crf','19','-pix_fmt','yuv420p','-c:a','aac','-ar','48000','-ac','2','-b:a','160k');if(audio)a.push('-af','volume=0.55,afade=t=in:d=0.08,afade=t=out:st='+(dur-.15)+':d=0.15');a.push(file);run(a);parts.push(file);timeline.push({name,start:cursor,duration:dur});cursor+=dur;console.log(name,cursor);
}
function concat(files,out){const list=p.join(work,p.basename(out)+'.txt');fs.writeFileSync(list,files.map(f=>"file '"+f.replaceAll('\\','/')+"'").join('\n'));run(['-f','concat','-safe','0','-i',list,'-c','copy',out]);}
const black=p.join(work,'black.mp4');run(['-f','lavfi','-i','color=c=0x080e19:s=1920x1080:r=30:d=8','-c:v','libx264','-pix_fmt','yuv420p',black]);
clip('01-room',src('room'),0,7,{overlay:'intro'});
clip('02-beach',src('beach'),.5,4);
clip('03-sea',src('sea'),.5,4);
clip('04-palace',src('palace'),.5,4);
clip('05-village',src('village'),.5,2);
clip('06-smoke',ed(6),0,7,{audio:true});
clip('07-medetashi',black,0,3.5,{overlay:'medetashi',fade:false});
const main=p.join(work,'main.mp4');concat(parts,main);
const freeze=p.join(work,'freeze.png');run(['-ss','31','-i',main,'-frames:v','1',freeze]);
clip('08-pause',freeze,0,2.5,{overlay:'pause',still:true,fade:false});
// Reverse the actual preceding story, ending on its beach scene.
clip('09-rewind',main,7,4,{overlay:'rewind',filter:'trim=duration=24.5,setpts=PTS-STARTPTS,reverse,setpts=PTS/6.125',fade:false});
clip('10-bully',src('bully'),0,5,{fade:false});
clip('11-question',src('bully'),1,5,{overlay:'question',filter:'eq=brightness=-0.27:saturation=0.35',fade:true});
clip('12-nine',black,0,3.5,{overlay:'nine'});
const shots=[[1,4],[2,15],[3,5],[4,25],[5,22],[6,11],[7,8],[8,47],[9,43]];
for(const[n,t]of shots)clip('ed'+n,ed(n),t,3.5,{overlay:'ed'+n,audio:true});
clip('final',src('room'),0,7,{overlay:'final',filter:'eq=brightness=-0.30:saturation=0.65',fade:true});
const picture=p.join(work,'picture.mp4');concat(parts,picture);
fs.writeFileSync(p.join(root,'timeline.json'),JSON.stringify(timeline,null,2));
// Original score: soft arpeggios, tape rewind, then a sparse pulse.
const rate=48000,total=cursor,buf=Buffer.alloc(Math.ceil(total*rate)*2+44);buf.write('RIFF');buf.writeUInt32LE(buf.length-8,4);buf.write('WAVEfmt ',8);buf.writeUInt32LE(16,16);buf.writeUInt16LE(1,20);buf.writeUInt16LE(1,22);buf.writeUInt32LE(rate,24);buf.writeUInt32LE(rate*2,28);buf.writeUInt16LE(2,32);buf.writeUInt16LE(16,34);buf.write('data',36);buf.writeUInt32LE(buf.length-44,40);
const note=[261.63,329.63,392,523.25,440,349.23,293.66,392];
for(let i=0;i<(buf.length-44)/2;i++){let t=i/rate,s=0;
 if(t<31.5){let k=Math.floor(t/.6),u=t% .6,f=note[k%8];s=.055*Math.sin(2*Math.PI*f*t)*Math.exp(-u*5)+.025*Math.sin(2*Math.PI*f/2*t);s*=Math.min(1,t/2,(31.5-t)/1.5);}
 else if(t>=34&&t<38){let u=t-34;s=.07*Math.sin(2*Math.PI*(550*t+350*u*u))*(.65+.35*Math.sin(t*110));}
 else if(t>=38&&t<48){s=.055*Math.sin(2*Math.PI*55*t)+.025*Math.sin(2*Math.PI*82.41*t);if(t<43){let u=(t-38)%1.25;s+=.14*Math.sin(2*Math.PI*90*u)*Math.exp(-u*26);}}
 else if(t>=51.5&&t<83){let u=(t-51.5)%.5;s=.095*Math.sin(2*Math.PI*65*u)*Math.exp(-u*16)+.025*Math.sin(2*Math.PI*130.81*t);}
 else if(t>=83){let u=t-83;s=.04*(Math.sin(2*Math.PI*261.63*t)+Math.sin(2*Math.PI*329.63*t)+Math.sin(2*Math.PI*392*t))*Math.min(1,u/1.5,(total-t)/2);}
 buf.writeInt16LE(Math.max(-32767,Math.min(32767,Math.round(s*32767))),44+i*2);}
const music=p.join(work,'score.wav');fs.writeFileSync(music,buf);
const voices=[['intro',.6],['question',44.2],['endings',48.5],['final',84.5]];
let args=['-i',picture,'-i',music];for(const[n]of voices)args.push('-i',p.join(root,'Voice',n+'.wav'));args.push('-i',p.resolve('Assets/_Project/Audio/Voice/Resources/Narration/medetashi.wav'));
let af='[0:a]volume=0.7[a0];[1:a]volume=0.85[a1];';voices.forEach(([n,t],i)=>af+=`[${i+2}:a]volume=1.5,adelay=${Math.round(t*1000)}:all=1[a${i+2}];`);af+='[6:a]volume=1.25,adelay=28300:all=1[a6];';af+='[a0][a1][a2][a3][a4][a5][a6]amix=inputs=7:duration=first:normalize=0,alimiter=limit=0.95[mix]';
args.push('-filter_complex',af,'-map','0:v:0','-map','[mix]','-c:v','copy','-c:a','aac','-b:a','192k','-movflags','+faststart',p.join(root,'Otogi_Trailer.mp4'));run(args);console.log('Completed',cursor);

