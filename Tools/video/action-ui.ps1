param([switch]$Contrast)
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PWD 'Exports/ActionTrailer/UI';New-Item -Force -ItemType Directory $dir | Out-Null
$white=[Drawing.Color]::FromArgb(255,245,242,232);$red=[Drawing.Color]::FromArgb(255,231,58,41)
function Canvas { $script:b=New-Object Drawing.Bitmap 1920,1080;$script:g=[Drawing.Graphics]::FromImage($b);$g.SmoothingMode='AntiAlias';$g.TextRenderingHint='AntiAliasGridFit' }
function Text($s,$size,$y,$color){$f=New-Object Drawing.Font 'Yu Gothic',([single]$size),([Drawing.FontStyle]::Bold),([Drawing.GraphicsUnit]::Pixel);$br=New-Object Drawing.SolidBrush $color;$fmt=New-Object Drawing.StringFormat;$fmt.Alignment='Center';$g.DrawString($s,$f,$br,(New-Object Drawing.RectangleF 40,([single]$y),1840,200),$fmt);$f.Dispose();$br.Dispose()}
function Save($name){$b.Save((Join-Path $dir ($name+'.png')),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$b.Dispose()}
function Card($name,$s,$small){Canvas;Text $small 25 342 $red;Text $s 88 440 $white;$g.FillRectangle((New-Object Drawing.SolidBrush $red),885,610,150,5);Save $name}
Card 'question' '本当にカメを助けたのでしょうか？' 'REWIND THE STORY'
Card 'choice' 'その選択が、運命を変える。' 'ONE CHOICE. EVERYTHING CHANGES.'
Card 'run' '逃げろ。' 'THE STORY IS WATCHING YOU'
Card 'nine' '結末は、ひとつじゃない。' '9 ENDINGS / YOUR CHOICE'
Canvas;Text 'めでたし、めでたし。' 66 400 $white;Text 'THE END' 22 570 $red;Save 'happy'
Canvas;$g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(150,4,8,14))),0,0,1920,1080)
Text 'PAUSED' 25 255 $red;$brush=New-Object Drawing.SolidBrush $white;$g.FillRectangle($brush,914,560,28,80);$g.FillRectangle($brush,979,560,28,80)
$g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(100,255,255,255))),140,862,1640,3);$g.FillRectangle((New-Object Drawing.SolidBrush $red),140,862,1530,3);$pauseClock=if($Contrast){'00:18    /    浦島太郎'}else{'00:12    /    浦島太郎'};Text $pauseClock 23 890 $white;Save 'pause'
Canvas;Text 'REWIND' 36 464 $white;Text '◀◀   16x' 23 530 $red;Save 'rewind'
Canvas;Text '童話配信' 160 305 $white;Text '物語を、選び直せ。' 53 550 $red;Text '9 ENDINGS  /  YOUR STORY' 24 680 $white;Text 'VOICEVOX：栗田まろん' 21 950 $white;Save 'title'
$labels=@('始まらない物語','亀の逆襲','ここまで準備したのに','禁じられた部屋','終わらない宴','玉手箱の煙','新しい人生','託された玉手箱','竜宮城からの脱出')
for($i=0;$i -lt 9;$i++){Canvas;$f=New-Object Drawing.Font 'Yu Gothic',26,([Drawing.FontStyle]::Bold),([Drawing.GraphicsUnit]::Pixel);$g.DrawString(('ED {0:00}  /  ' -f ($i+1))+$labels[$i],$f,(New-Object Drawing.SolidBrush $white),96,982);$g.FillRectangle((New-Object Drawing.SolidBrush $red),65,985,7,34);Save ('ed'+($i+1))}

