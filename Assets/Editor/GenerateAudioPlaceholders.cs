using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyGame.Editor
{
    public static class GenerateAudioPlaceholders
    {
        [MenuItem("Tools/Audio/Generate Placeholder Music")]
        public static void GeneratePlaceholders()
        {
            string musicFolder = Path.Combine(Application.dataPath, "Audio/Music");
            if (!Directory.Exists(musicFolder))
            {
                Directory.CreateDirectory(musicFolder);
            }

            // Tạo 3 track nhạc hình sin khác tần số và thời lượng
            CreateSineWavFile(Path.Combine(musicFolder, "bgm_track_1.wav"), 330f, 4f, 44100);  // Nốt E4, 4 giây
            CreateSineWavFile(Path.Combine(musicFolder, "bgm_track_2.wav"), 440f, 5f, 44100);  // Nốt A4, 5 giây
            CreateSineWavFile(Path.Combine(musicFolder, "bgm_track_3.wav"), 554f, 6f, 44100);  // Nốt C#5, 6 giây

            AssetDatabase.Refresh();
            Debug.Log("[AudioGenerator] Da tao thanh cong 3 file nhạc nền mẫu (.wav) trong Assets/Audio/Music/");
        }

        private static void CreateSineWavFile(string filepath, float frequency, float duration, int sampleRate)
        {
            int numSamples = Mathf.RoundToInt(sampleRate * duration);
            short[] pcmData = new short[numSamples];
            float amplitude = 0.15f; // Âm lượng nhỏ vừa phải để tránh chói tai

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Tạo sóng hình sin cơ bản
                float sampleValue = Mathf.Sin(2f * Mathf.PI * frequency * t);
                
                // Áp dụng fade-in và fade-out nhỏ ở 2 đầu để tránh tiếng click bụp khi bắt đầu/kết thúc
                float fadeDuration = 0.2f; // 0.2 giây
                float fadeFactor = 1.0f;
                if (t < fadeDuration)
                {
                    fadeFactor = t / fadeDuration;
                }
                else if (duration - t < fadeDuration)
                {
                    fadeFactor = (duration - t) / fadeDuration;
                }

                pcmData[i] = (short)(sampleValue * amplitude * fadeFactor * 32767);
            }

            using (FileStream fs = new FileStream(filepath, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                // 1. RIFF Header
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + pcmData.Length * 2); // Kích thước còn lại của file
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });

                // 2. Format Chunk
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16); // Kích thước SubChunk1 (16 cho PCM)
                writer.Write((short)1); // Audio Format (1 = PCM)
                writer.Write((short)1); // Số kênh (1 = Mono)
                writer.Write(sampleRate); // Sample Rate (44100)
                writer.Write(sampleRate * 2); // Byte Rate (SampleRate * NumChannels * BitsPerSample/8)
                writer.Write((short)2); // Block Align (NumChannels * BitsPerSample/8)
                writer.Write((short)16); // Bits Per Sample (16)

                // 3. Data Chunk
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(pcmData.Length * 2); // Kích thước SubChunk2 (NumSamples * NumChannels * BitsPerSample/8)

                // Ghi dữ liệu PCM 16-bit
                for (int i = 0; i < pcmData.Length; i++)
                {
                    writer.Write(pcmData[i]);
                }
            }
        }
    }
}
