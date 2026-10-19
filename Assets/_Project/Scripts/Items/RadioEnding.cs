using ProjectRidge.Core;

namespace ProjectRidge.Items
{
    /// <summary>La radio de la camioneta: usarla termina el PoC (secuencia final del GameFlow).</summary>
    public class RadioEnding : Interactable
    {
        public override string Prompt => "Use the radio";

        public override void Interact(ProjectRidge.Player.PlayerInteractor who)
        {
            if (GameFlow.Instance != null) GameFlow.Instance.TriggerEnding();
        }
    }
}
