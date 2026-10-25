// Original procedural SE: a dry wooden stick knocking on a turtle shell ("コツッ・ゴン").
// Bright click of the stick + short ringing of the wood + a low, hollow thunk of the shell.
const fs = require('node:fs');
const path = require('node:path');
const rate = 44100, seconds = .4, count = Math.round(rate * seconds);
let seed = 0x5717c;
function noise() { seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5; return (seed >>> 0) / 2147483648 - 1; }
const samples = new Float32Array(count);
// Damped modes: [frequency Hz, decay seconds, level]. Slightly inharmonic so it reads as wood, not a bell.
const stick = [[1180, .035, .55], [1930, .022, .32], [2870, .014, .2], [760, .05, .25]];
const shell = [[205, .11, .9], [318, .075, .5], [470, .05, .3]];
let hp = 0, lp = 0;
for (let i = 0; i < count; i++) {
  const t = i / rate;
  // Contact click: 3 ms of high-passed noise.
  const n = noise();
  hp += .55 * (n - hp);
  const click = (n - hp) * Math.exp(-t / .0012) * 1.1;
  let ring = 0;
  for (const [f, d, a] of stick) ring += a * Math.sin(2 * Math.PI * f * t) * Math.exp(-t / d);
  let body = 0;
  // The shell's pitch sags a little as the knock settles, like a hollow box.
  for (const [f, d, a] of shell) body += a * Math.sin(2 * Math.PI * f * t * (1 - .04 * Math.min(1, t / .08))) * Math.exp(-t / d);
  // A soft breathy tail from the hollow shell.
  lp += .08 * (n - lp);
  const air = lp * Math.exp(-t / .06) * .35;
  const attack = Math.min(1, t / .0006);
  samples[i] = attack * (click + ring * .7 + body + air);
}
let peak = 0, squares = 0;
for (const s of samples) { peak = Math.max(peak, Math.abs(s)); squares += s * s; }
const gain = .85 / peak;
// Fade the last 20 ms to silence so the clip never ends with a click.
const fade = Math.round(rate * .02);
for (let i = 0; i < fade; i++) samples[count - 1 - i] *= i / fade;
const bytes = count * 2, wav = Buffer.alloc(44 + bytes);
wav.write('RIFF'); wav.writeUInt32LE(36 + bytes, 4); wav.write('WAVEfmt ', 8);
wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(1, 22);
wav.writeUInt32LE(rate, 24); wav.writeUInt32LE(rate * 2, 28);
wav.writeUInt16LE(2, 32); wav.writeUInt16LE(16, 34); wav.write('data', 36); wav.writeUInt32LE(bytes, 40);
for (let i = 0; i < count; i++) wav.writeInt16LE(Math.round(Math.max(-1, Math.min(1, samples[i] * gain)) * 32767), 44 + i * 2);
const output = path.resolve(__dirname, '../../Assets/Resources/Audio/StickHit.wav');
fs.mkdirSync(path.dirname(output), { recursive: true }); fs.writeFileSync(output, wav);
const report = { seconds, sampleRate: rate, channels: 1, peak: peak * gain, rms: Math.sqrt(squares / count) * gain,
  endSample: samples[count - 1] * gain, bytes: wav.length };
if (report.peak > .86 || report.rms < .05 || Math.abs(report.endSample) > .001) throw new Error(JSON.stringify(report));
fs.writeFileSync(path.join(__dirname, 'StickHit.validation.json'), JSON.stringify(report, null, 2) + '\n');
console.log('STICK_HIT_OK', JSON.stringify(report));
