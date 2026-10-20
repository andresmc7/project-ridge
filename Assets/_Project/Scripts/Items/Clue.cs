using ProjectRidge.Core;
using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>Pista narrativa que se inspecciona con Interact: texto breve, sin resolver el misterio.</summary>
    public class Clue : Interactable
    {
        public override Kind ActionKind => Kind.Inspect;
        [SerializeField] private string displayName = "Photograph";
        [SerializeField] private string title = "A PHOTOGRAPH";
        [TextArea(2, 6)] [SerializeField] private string body = "";
        [Tooltip("Objetivo nuevo al leerla (vacío = no cambia).")]
        [SerializeField] private string objectiveAfter = "";

        public bool Read { get; private set; }
        public override string Prompt => "Inspect " + displayName;

        public override void Interact(ProjectRidge.Player.PlayerInteractor who)
        {
            var flow = GameFlow.Instance;
            if (flow == null) return;
            Read = true;
            flow.ShowClue(title, body);
            flow.SetObjective(objectiveAfter);
        }
    }
}
