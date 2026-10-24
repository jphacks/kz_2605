// Original procedural surf: no external recordings or runtime synthesis needed.
const fs = require('node:fs');
const path = require('node:path');
const rate = 24000, seconds = 24, count = rate * seconds, overlap = rate * 2;
let seed = 0x5ea7a;
function noise() { seed ^= seed << 13; seed ^= seed >>> 17; seed ^= seed << 5; return (seed >>> 0) / 2147483648 - 1; }
const channels = [new Float32Array(count + overlap), new Float32Array(count + overlap)];
const states = Array.from({ length: 3 }, () => ({ low: 0, mid: 0, high: 0 }));
function seaNoise(state) {
  const n = noise();
  state.low += .009 * (n - state.low);
  state.mid += .07 * (n - state.mid);
  state.high += .4 * (n - state.high);
  return state.low * 1.8 + state.mid * .8 + (n - state.high) * .13;
}
for (let i = -rate; i < count + overlap; i++) {
  const phase = 2 * Math.PI * i / count;
  const swell = Math.pow(.5 + .5 * Math.sin(phase * 3 + .28 * Math.sin(phase)), 2.4);
  const backwash = Math.pow(.5 + .5 * Math.sin(phase * 4 - 1.3), 3);
  const envelope = .24 + .65 * swell + .13 * backwash;
  const shared = seaNoise(states[0]);
  for (let channel = 0; channel < 2; channel++) {
    const sample = (shared * .86 + seaNoise(states[channel + 1]) * .23) * envelope;
    if (i >= 0) channels[channel][i] = sample;
  }
}
let peak = 0, squares = 0;
for (const samples of channels) for (let i = 0; i < count; i++) {
  if (i < overlap) {
    const fade = i / overlap * Math.PI * .5;
    samples[i] = samples[i] * Math.sin(fade) + samples[count + i] * Math.cos(fade);
  }
  peak = Math.max(peak, Math.abs(samples[i])); squares += samples[i] ** 2;
}
const gain = Math.min(.72 / peak, .16 / Math.sqrt(squares / (count * 2)));
const bytes = count * 4, wav = Buffer.alloc(44 + bytes);
wav.write('RIFF'); wav.writeUInt32LE(36 + bytes, 4); wav.write('WAVEfmt ', 8);
wav.writeUInt32LE(16, 16); wav.writeUInt16LE(1, 20); wav.writeUInt16LE(2, 22);
wav.writeUInt32LE(rate, 24); wav.writeUInt32LE(rate * 4, 28);
wav.writeUInt16LE(4, 32); wav.writeUInt16LE(16, 34); wav.write('data', 36); wav.writeUInt32LE(bytes, 40);
for (let i = 0; i < count; i++) for (let channel = 0; channel < 2; channel++)
  wav.writeInt16LE(Math.round(channels[channel][i] * gain * 32767), 44 + i * 4 + channel * 2);
const output = path.resolve(__dirname, '../../Assets/Resources/Audio/OceanWaves.wav');
fs.mkdirSync(path.dirname(output), { recursive: true }); fs.writeFileSync(output, wav);
const report = { seconds, sampleRate: rate, channels: 2, peak: peak * gain,
  rms: Math.sqrt(squares / (count * 2)) * gain, bytes: wav.length,
  loopBoundaryStep: channels.map(samples => Math.abs(samples[0] - samples[count - 1]) * gain) };
if (report.peak > .73 || report.rms < .03 || report.loopBoundaryStep.some(step => step > .12)) throw new Error(JSON.stringify(report));
fs.writeFileSync(path.join(__dirname, 'OceanWaves.validation.json'), JSON.stringify(report, null, 2) + '\n');
console.log('OCEAN_AUDIO_OK', JSON.stringify(report));
