// Insert a short, self-contained feature demonstration into the approved contrast edit.
const fs=require('fs'),p=require('path'),{spawnSync}=require('child_process');
const root=p.resolve('Exports/ActionTrailer'),edit=p.join(root,'Edit'),input=p.join(root,'Otogi_Trailer_Contrast.mp4');
function ff(args){const r=spawnSync('ffmpeg',['-hide_banner','-loglevel','error','-y',...args],{stdio:'inherit'});if(r.status!==0)throw Error('ffmpeg failed');}
const cut=53.4,duration=8,output=p.join(root,'Otogi_Trailer_Mobile.mp4');
const args=['-i',input];
for(const name of ['vote','voted','comment','sent'])args.push('-loop','1','-framerate','30','-t','2','-i',p.join(root,'UI','mobile-'+name+'.png'));
args.push('-i',p.join(edit,'score.wav'));
let filter='';
for(let i=1;i<=4;i++)filter+=`[${i}:v]setsar=1,format=yuv420p,setpts=PTS-STARTPTS[m${i}];`;
filter+='[m1][m2][m3][m4]concat=n=4:v=1:a=0[demo];';
filter+=`[0:v]split=2[head][tail];[head]trim=end=${cut},setpts=PTS-STARTPTS[h];[tail]trim=start=${cut},setpts=PTS-STARTPTS[t];[h][demo][t]concat=n=3:v=1:a=0[v];`;
filter+=`[0:a]asplit=2[ah][at];[ah]atrim=end=${cut},asetpts=PTS-STARTPTS[ahead];[at]atrim=start=${cut},asetpts=PTS-STARTPTS[atail];`;
filter+='[5:a]atrim=start=6:end=14,asetpts=PTS-STARTPTS,volume=0.55,afade=t=in:d=0.25,afade=t=out:st=7.5:d=0.5,pan=stereo|c0=c0|c1=c0[bed];[ahead][bed][atail]concat=n=3:v=0:a=1[a]';
args.push('-filter_complex',filter,'-map','[v]','-map','[a]','-c:v','libx264','-preset','fast','-crf','18','-pix_fmt','yuv420p','-r','30','-c:a','aac','-b:a','192k','-ar','48000','-ac','2','-movflags','+faststart',output);ff(args);
const timeline=JSON.parse(fs.readFileSync(p.join(root,'contrast-timeline.json'),'utf8'));
const revised=timeline.flatMap(x=>x.name==='25-silence'?[{name:'mobile-vote',start:cut,duration:4},{name:'mobile-comment',start:cut+4,duration:4},{...x,start:x.start+duration}]:[{...x,start:x.start>=cut-0.001?x.start+duration:x.start}]);
fs.writeFileSync(p.join(root,'mobile-timeline.json'),JSON.stringify(revised,null,2));
console.log(output);
