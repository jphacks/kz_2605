Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PWD 'Exports/ActionTrailer/UI'
function Label($text,$size,$x,$y,$color){
 $font=[Drawing.Font]::new('Yu Gothic',$size,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
 $brush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($color))
 $g.DrawString($text,$font,$brush,[single]$x,[single]$y);$font.Dispose();$brush.Dispose()
}
function Box($x,$y,$w,$h,$color){$brush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($color));$g.FillRectangle($brush,$x,$y,$w,$h);$brush.Dispose()}
foreach($state in @('vote','voted','comment','sent')){
 $b=[Drawing.Bitmap]::new(1920,1080);$g=[Drawing.Graphics]::FromImage($b);$g.SmoothingMode='AntiAlias';$g.TextRenderingHint='AntiAliasGridFit';$g.Clear([Drawing.ColorTranslator]::FromHtml('#141321'))
 Label 'スマホから、物語に参加。' 66 130 190 '#f7efdc'
 Label 'アプリ不要。ブラウザで観戦。' 32 135 300 '#d9a441'
 if($state -in @('vote','voted')){
  Label '選択肢をタップして投票' 47 135 450 '#f7efdc'
  Label '体験者が迷って時間切れになったら' 30 135 545 '#cfc6da'
  Label 'みんなの投票1位で、物語が進む。' 30 135 595 '#cfc6da'
 }else{
  Label 'コメントで、応援。' 47 135 450 '#f7efdc'
  Label '送った言葉が、VR空間にも流れる。' 30 135 545 '#cfc6da'
  if($state -eq 'sent'){Box 135 665 725 94 '#332a46';Label 'がんばれ、浦島太郎！' 39 160 680 '#f7efdc'}
 }
 Box 1140 65 580 950 '#080910';Box 1154 80 552 918 '#2a2140';Box 1330 88 190 18 '#080910'
 Label '童話配信' 30 1180 125 '#f7efdc';Label 'LIVE' 20 1580 134 '#ef6c60'
 $scene=[Drawing.Image]::FromFile((Join-Path $dir 'mobile-scene.jpg'));$g.DrawImage($scene,1174,190,512,288);$scene.Dispose()
 Label '海辺 ― 物語をいっしょに見守ろう' 20 1180 490 '#f7efdc'
 Box 1174 550 512 220 '#fffaf0';Label '現在の投票' 19 1190 560 '#8f2a1f';Label 'カメをどうする？' 28 1190 593 '#3a2a1e'
 $selected=$state -ne 'vote';$button=if($selected){'#8f2a1f'}else{'#efe2c4'};$ink=if($selected){'#fffaf0'}else{'#3a2a1e'}
 Box 1190 644 232 56 $button;Label $(if($selected){'助ける  ✓'}else{'助ける'}) 24 1204 652 $ink
 Box 1434 644 232 56 '#efe2c4';Label '見過ごす' 24 1448 652 '#3a2a1e'
 Label $(if($selected){'投票済み · 締め切りまで変更できます'}else{'好きな選択肢を1つタップ'}) 18 1190 720 '#7a6450'
 Box 1174 790 512 172 '#fffaf0';Label 'コメント' 23 1190 800 '#3a2a1e';Box 1190 844 380 48 '#efe2c4'
 Label $(if($state -in @('comment','sent')){'がんばれ、浦島太郎！'}else{'コメントする'}) 21 1200 854 '#3a2a1e'
 Box 1580 844 88 48 '#8f2a1f';Label '送る' 22 1595 853 '#fffaf0'
 if($state -eq 'sent'){Label 'あなた：がんばれ、浦島太郎！' 20 1190 913 '#8f2a1f'}
 Label '画面は機能紹介用のデモです' 20 1195 1018 '#cfc6da'
 $b.Save((Join-Path $dir ('mobile-'+$state+'.png')),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$b.Dispose()
}
