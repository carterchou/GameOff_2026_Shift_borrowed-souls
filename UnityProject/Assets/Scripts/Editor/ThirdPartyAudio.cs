using UnityEditor;
using UnityEngine;

namespace BorrowedSouls.EditorTools
{
    /// <summary>Import rules for the licensed audio, which isn't in this repository: music streams, voices load small and mono.</summary>
    public sealed class ThirdPartyAudio : AssetPostprocessor
    {
        private const string Root = "Assets/Audio/ThirdParty/";

        public override uint GetVersion() => 2;

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (AudioImporter)assetImporter;
            bool voice = assetPath.StartsWith(Root + "SoundEffectLab/");
            importer.forceToMono = voice;
            importer.loadInBackground = false;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.preloadAudioData = voice;
            s.loadType = voice ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = voice ? 0.6f : 0.7f;
            importer.defaultSampleSettings = s;
        }
    }
}
