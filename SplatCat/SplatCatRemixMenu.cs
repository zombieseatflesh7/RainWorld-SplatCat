using UnityEngine;
using Menu.Remix.MixedUI;
using System.Runtime.CompilerServices;
using Menu.Remix.MixedUI.ValueTypes;

namespace SplatCat
{
    public class SplatCatRemixMenu : OptionInterface
    {
        public struct SquishPreset
        {
            public SquishPreset(string name, float squishMultiplier, float durationMultiplier, float frequencyMultiplier)
            {
                this.name = name;
                this.squishMultiplier = squishMultiplier;
                this.durationMultiplier = durationMultiplier;
                this.frequencyMultiplier = frequencyMultiplier;
            }

            public string name;
            public float squishMultiplier;
            public float durationMultiplier;
            public float frequencyMultiplier;
        }

        public static SquishPreset[] presets = new SquishPreset[]
        {
            new SquishPreset("Default",       1f,    1f,    1f   ),
            new SquishPreset("Jello",         0.6f,  2f,    1.3f ),
            new SquishPreset("Dough",         1.5f,  3f,    0f   ),
            new SquishPreset("Water Balloon", 1.7f,  0.85f, 0.75f),
            new SquishPreset("Bouncy Ball",   1.25f, 0.7f,  1.5f ),
            new SquishPreset("Slow-mo",       1.5f,  3f,    0.25f),
            new SquishPreset("Tuning Fork",   0.5f,  3f,    5f   ),
        };

        public readonly Configurable<float> intensity;
        public readonly Configurable<float> duration;
        public readonly Configurable<float> frequency;

        private OpSimpleButton[] presetButtons;
        private OpFloatSlider[] sliders;

        public SplatCatRemixMenu(SplatCat plugin)
        {
            intensity = this.config.Bind<float>("SplatCat_Intensity", 1f);
            duration = this.config.Bind<float>("SplatCat_Duration", 1f);
            frequency = this.config.Bind<float>("SplatCat_Frequency", 1f);

            ConfigOnChange();
            typeof(OptionInterface).GetEvent("OnConfigChanged").GetAddMethod().Invoke(this, new object[] { (OnEventHandler)ConfigOnChange });
            typeof(OptionInterface).GetEvent("OnUnload").GetAddMethod().Invoke(this, new object[] { (OnEventHandler)Unload });
        }

        public override void Initialize()
        {
            this.Tabs = new OpTab[] { new OpTab(this, "Config") };

            // Presets label
            this.Tabs[0].AddItems(new OpLabel(new Vector2(25f, 500f), new Vector2(200f, 50f), "PRESETS", FLabelAlignment.Center, true));

            // Individual presets
            presetButtons = new OpSimpleButton[presets.Length];
            Vector2 pos = new Vector2(40f, 445f);
            for (int i = 0; i < presets.Length; i++)
            {
                presetButtons[i] = new OpSimpleButton(pos, new Vector2(170f, 30f), presets[i].name);
                typeof(OpSimpleButton).GetEvent("OnClick").GetAddMethod().Invoke(presetButtons[i], new object[] { (OnSignalHandler)Signal });
                pos.y -= 45;
            }
            this.Tabs[0].AddItems(presetButtons);

            // Sliders
            sliders = new OpFloatSlider[3];
            Configurable<float>[] configs = { intensity, duration, frequency };
            string[] sliderNames = new string[] { "Intensity", "Duration", "Frequency" };
            pos.Set(375f, 300f + sliderNames.Length * 45f * 0.5f - 30f * 0.5f);
            for (int i = 0; i < 3; i++)
            {
                pos.x = 255f;
                OpLabel label = new OpLabel(pos, new Vector2(110f, 30f), sliderNames[i], FLabelAlignment.Right);
                pos.x = 375;
                sliders[i] = new OpFloatSlider(configs[i], pos + Vector2.up * 3f, 200, 2) { min = 0, max = 5, hideLabel = false, description = sliderNames[i] };
                this.Tabs[0].AddItems( label, sliders[i]);
                pos.y -= 45f;
            }
        }

        public void Unload()
        {
            presetButtons = null;
            sliders = null;
        }

        public void ConfigOnChange()
        {
            SplatCat.squishMultiplier = intensity.Value;
            SplatCat.squishDurationMultiplier = duration.Value;
            SplatCat.squishFrequencyMultiplier = frequency.Value;
        }

        public void Signal(UIfocusable trigger)
        {
            int i = presetButtons.IndexOf(trigger as OpSimpleButton);
            if (i == -1) return;
            SquishPreset p = presets[i];
            sliders[0].SetValueFloat(p.squishMultiplier);
            sliders[1].SetValueFloat(p.durationMultiplier);
            sliders[2].SetValueFloat(p.frequencyMultiplier);
        }
    }
}
