Add-Type -AssemblyName System.Drawing
$outDir=Join-Path $PWD 'Exports/Trailer/UI'
New-Item -ItemType Directory -Force $outDir | Out-Null
$white=[Drawing.Color]::FromArgb(255,247,243,229)
$gold=[Drawing.Color]::FromArgb(255,226,191,124)
function New-Canvas {
 $script:bmp=New-Object Drawing.Bitmap 1920,1080
 $script:g=[Drawing.Graphics]::FromImage($bmp)
 $g.SmoothingMode='AntiAlias';$g.TextRenderingHint='AntiAliasGridFit'
}
function Text-Centered($text,$size,$y,$color) {
 $font=New-Object Drawing.Font 'Yu Gothic',([single]$size),([Drawing.FontStyle]::Bold),([Drawing.GraphicsUnit]::Pixel)
 $brush=New-Object Drawing.SolidBrush $color
 $fmt=New-Object Drawing.StringFormat;$fmt.Alignment='Center';$fmt.LineAlignment='Center'
 $rect=New-Object Drawing.RectangleF 70,([single]$y),1780,([single]($size*1.9))
 $g.DrawString($text,$font,$brush,$rect,$fmt);$font.Dispose();$brush.Dispose();$fmt.Dispose()
}
function Save-Canvas($name){$bmp.Save((Join-Path $outDir ($name+'.png')),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()}
function Card($name,$line,$size=65){New-Canvas;Text-Centered $line $size 450 $white;Save-Canvas $name}
New-Canvas
$shade=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(155,9,15,24));$g.FillRectangle($shade,0,245,1920,185)
Text-Centered 'もし、絵本の世界に入れたら…' 63 280 $white
Save-Canvas 'intro'
New-Canvas;Text-Centered 'めでたし、めでたし。' 78 280 $white;Save-Canvas 'medetashi'
Card 'question' '本当にカメを助けたのでしょうか？' 66
New-Canvas
Text-Centered 'ひとつの選択が、' 50 405 $white
Text-Centered '9つの結末へ。' 94 490 $gold
Save-Canvas 'nine'
New-Canvas
Text-Centered '童話配信' 128 255 $white
Text-Centered 'あなたなら、どう選ぶ？' 66 510 $gold
Text-Centered '次の物語を選ぶのは、あなたです。' 37 670 $white
Text-Centered 'VOICEVOX：栗田まろん' 23 966 $white
Save-Canvas 'final'
New-Canvas
$shade=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(125,5,9,16));$g.FillRectangle($shade,0,0,1920,1080)
$brush=New-Object Drawing.SolidBrush $white
$g.FillRectangle($brush,907,440,36,125);$g.FillRectangle($brush,977,440,36,125)
Text-Centered '一時停止' 32 588 $white
$font=New-Object Drawing.Font 'Yu Gothic',30,([Drawing.FontStyle]::Bold),([Drawing.GraphicsUnit]::Pixel)
$g.DrawString('OTOGI  /  浦島太郎',$font,$brush,90,75)
$g.FillRectangle((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(120,255,255,255))),100,937,1720,5)
$g.FillRectangle((New-Object Drawing.SolidBrush $gold),100,937,1590,5)
$g.FillEllipse((New-Object Drawing.SolidBrush $gold),1679,929,20,20)
$g.DrawString('00:32  /  00:34',$font,$brush,100,972)
Save-Canvas 'pause'
New-Canvas
$shade=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(80,5,15,30));$g.FillRectangle($shade,0,0,1920,1080)
$brush=New-Object Drawing.SolidBrush $white
$points1=[Drawing.Point[]]@((New-Object Drawing.Point 865,500),(New-Object Drawing.Point 955,448),(New-Object Drawing.Point 955,552))
$points2=[Drawing.Point[]]@((New-Object Drawing.Point 965,500),(New-Object Drawing.Point 1055,448),(New-Object Drawing.Point 1055,552))
$g.FillPolygon($brush,$points1);$g.FillPolygon($brush,$points2)
Text-Centered 'REWIND  /  8x' 34 590 $white
Save-Canvas 'rewind'
$titles=@('始まらない物語','亀の逆襲','ここまで準備したのに','禁じられた部屋','終わらない宴','玉手箱の煙','新しい人生','託された玉手箱','竜宮城からの脱出')
for($i=0;$i -lt 9;$i++){
 New-Canvas
 $shade=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(210,7,12,21));$g.FillRectangle($shade,75,72,900,139)
 $font=New-Object Drawing.Font 'Yu Gothic',22,([Drawing.FontStyle]::Bold),([Drawing.GraphicsUnit]::Pixel)
 $g.DrawString(('ENDING  {0:00} / 09' -f ($i+1)),$font,(New-Object Drawing.SolidBrush $gold),106,86)
 $font=New-Object Drawing.Font 'Yu Gothic',46,([Drawing.FontStyle]::Bold),([Drawing.GraphicsUnit]::Pixel)
 $g.DrawString($titles[$i],$font,(New-Object Drawing.SolidBrush $white),103,124)
 Save-Canvas ('ed'+($i+1))
}



