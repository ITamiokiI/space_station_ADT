using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.ADT.SpeechBarks;
using Content.Shared.Chat;
using Content.Shared.Corvax.CCCVars;
using Robust.Client.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using System.Threading.Tasks;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;
using Robust.Client.Player;
using Content.Shared.ADT.CCVar;

namespace Content.Client.ADT.SpeechBarks;

public sealed class SpeechBarksSystem : SharedSpeechBarksSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(CCCVars.TTSVolume, OnVolumeChanged, true);

        SubscribeNetworkEvent<PlaySpeechBarksEvent>(OnEntitySpoke);
    }

    private readonly List<string> _sampleText =
    new()
    {
            "Тест мессЭдж 1.",
            "Тест мессЭдж 2!",
            "Тест мессЭдж 3?",
            "Здесь был котя."
    };

    private const float MinimalVolume = -10f;
    private float _volume = 0.0f;
    private const float WhisperFade = 4f;

    private void OnVolumeChanged(float volume)
    {
        _volume = volume;
    }

    private float AdjustVolume(bool isWhisper)
    {
        var volume = MinimalVolume + SharedAudioSystem.GainToVolume(_volume);

        if (isWhisper)
        {
            volume -= SharedAudioSystem.GainToVolume(WhisperFade);
        }

        return volume;
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? SharedChatSystem.WhisperMuffledRange : SharedChatSystem.VoiceRange;
    }

    private async void OnEntitySpoke(PlaySpeechBarksEvent ev)
    {
        if (_cfg.GetCVar(ADTCCVars.ReplaceTTSWithBarks) == false)
            return;

        if (ev.Message == null)
            return;

        if (ev.Source != null)
        {
            var audioParams = AudioParams.Default
                .WithMaxDistance(AdjustDistance(ev.IsWhisper))
                .WithPitchScale(ev.Pitch)
                .WithVolume(AdjustVolume(ev.IsWhisper));

            if (ev.Message.EndsWith('!'))
                audioParams = audioParams.WithRolloffFactor(1.4f);
            //audioParams = audioParams.WithVolume(audioParams.Volume * 2.5f);

            var count = Math.Clamp((int)ev.Message.Length / 3f, 1, 15);
            var message = ev.Message;
            var audioResource = new AudioResource();
            string str = ev.Sound;

            var path = new ResPath(str);
            audioResource.Load(IoCManager.Instance!, path);

            for (var i = 0; i < count; i++)
            {
                if (_player.LocalSession == null)
                    break;

                _audio.PlayEntity(audioResource.AudioStream, GetEntity(ev.Source.Value), audioParams.WithPitchScale(_random.NextFloat(ev.Pitch - 0.1f, ev.Pitch + 0.1f)));

                await Task.Delay(TimeSpan.FromSeconds(_random.NextFloat(ev.LowVar, ev.HighVar)));
            }
        }
    }

    public async void PlayDataPrewiew(string protoId, float pitch, float lowVar, float highVar)
    {
        if (!_proto.TryIndex<BarkPrototype>(protoId, out var proto))
            return;

        var message = _random.Pick(_sampleText);

        var audioParams = AudioParams.Default
            .WithVolume(AdjustVolume(false));

        var count = (int)message.Length / 3f;
        var audioResource = new AudioResource();
        string str = proto.Sound;

        if (message.EndsWith('!'))
            audioParams = audioParams.WithRolloffFactor(1.4f);

        var path = new ResPath(str);
        audioResource.Load(IoCManager.Instance!, path);

        for (var i = 0; i < count; i++)
        {
            if (_player.LocalSession == null)
                break;

            _audio.PlayGlobal(str, _player.LocalSession, audioParams.WithPitchScale(_random.NextFloat(pitch - 0.1f, pitch + 0.1f)));

            await Task.Delay(TimeSpan.FromSeconds(_random.NextFloat(lowVar, highVar)));
        }
    }


}
