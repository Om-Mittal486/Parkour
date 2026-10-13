$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.IO;
public static class StillworksScore {
    public static void Write(string path) {
        const int rate = 22050, seconds = 64;
        int[][] chords = { new[]{50,57,65}, new[]{46,53,62}, new[]{48,55,64}, new[]{45,52,60},
                           new[]{50,57,65}, new[]{43,50,58}, new[]{46,53,62}, new[]{45,52,60} };
        int[] melody = {74,77,81,77,72,74,69,65};
        using (var stream = File.Create(path)) using (var writer = new BinaryWriter(stream)) {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + rate * seconds * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
            writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(rate * seconds * 2);
            for (int i = 0; i < rate * seconds; i++) {
                double t = (double)i / rate, phrase = t % 8 / 8;
                int chord = (int)(t / 8);
                double swell = Math.Pow(Math.Sin(Math.PI * phrase), .6), sample = 0;
                foreach (int note in chords[chord]) {
                    double f = 440 * Math.Pow(2,(note - 69) / 12.0);
                    sample += .045 * swell * (Math.Sin(t * f * 2 * Math.PI) + .18 * Math.Sin(t * f * 4 * Math.PI));
                }
                double m = 440 * Math.Pow(2,(melody[((int)(t / 2) + chord) % 8] - 69) / 12.0);
                double age = t % 2, attack = Math.Min(1,age * 25);
                sample += .13 * Math.Sin(t * m * 2 * Math.PI) * Math.Exp(-age * 2.4) * attack;
                sample *= Math.Min(1,t / .5) * Math.Min(1,(seconds - t) / .7);
                writer.Write((short)(Math.Max(-1,Math.Min(1,sample)) * 32767));
            }
        }
    }
}
'@
$projectPath = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $projectPath 'Assets/Stillworks/Audio/Last Wash.wav'
[StillworksScore]::Write($destination)
Write-Output 'Created the original 64-second Last Wash soundtrack.'
