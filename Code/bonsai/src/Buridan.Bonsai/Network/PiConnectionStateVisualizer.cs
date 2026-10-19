// =============================================================================
// PiConnectionStateVisualizer.cs
//
// Shows a PiConnectionState as a coloured panel: green when the Pi answers,
// red when it does not. Works on its own or inside a Bonsai.Gui layout.
// =============================================================================
using System;
using System.Drawing;
using System.Windows.Forms;
using Bonsai;
using Bonsai.Design;
using Buridan.Bonsai;

[assembly: TypeVisualizer(typeof(PiConnectionStateVisualizer), Target = typeof(PiConnectionState))]

namespace Buridan.Bonsai
{
    /// <summary>
    /// Provides a type visualizer that displays the Pi connection state as a
    /// coloured status panel.
    /// </summary>
    public class PiConnectionStateVisualizer : DialogTypeVisualizer
    {
        static readonly Color ConnectedColor = Color.FromArgb(198, 239, 206);
        static readonly Color DisconnectedColor = Color.FromArgb(255, 199, 206);

        Label label;

        /// <inheritdoc/>
        public override void Load(IServiceProvider provider)
        {
            label = new Label();
            label.Dock = DockStyle.Fill;
            label.Size = new Size(420, 64);
            label.Padding = new Padding(6);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = new Font(FontFamily.GenericSansSerif, 10.0f);
            label.BackColor = SystemColors.Control;
            label.Text = "Waiting for the first connection check...";

            IDialogTypeVisualizerService visualizerService =
                (IDialogTypeVisualizerService)provider.GetService(typeof(IDialogTypeVisualizerService));
            if (visualizerService != null)
            {
                visualizerService.AddControl(label);
            }
        }

        /// <inheritdoc/>
        public override void Show(object value)
        {
            PiConnectionState state = value as PiConnectionState;
            if (state == null)
            {
                return;
            }
            label.Text = state.ToString();
            if (state.IsConnected)
            {
                label.BackColor = ConnectedColor;
            }
            else
            {
                label.BackColor = DisconnectedColor;
            }
        }

        /// <inheritdoc/>
        public override void Unload()
        {
            if (label != null)
            {
                label.Dispose();
                label = null;
            }
        }
    }
}
