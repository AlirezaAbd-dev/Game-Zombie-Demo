using System;
using Godot;
using TopDownDemo.Core;
using TopDownDemo.Entities.Components;
using TopDownDemo.Systems;
using TopDownDemo.UI;

namespace TopDownDemo.Entities;

public partial class The_Archivist : NPC
{
    private bool _hasGivenJournal = false;
    private PointLight2D _spectralLight;

    public override void _Ready()
    {
        _hasGivenJournal = StoryDirector.Instance?.HasFlag(StoryFlags.ARCHIVIST_JOURNAL_COLLECTED) == true;
        CharacterName = "The Archivist";
        PromptText = "LISTEN";
        CharacterColor = new Color(0.8f, 0.5f, 1.0f);

        base._Ready();

        // Spectral glow light
        _spectralLight = new PointLight2D
        {
            Color = new Color(0.75f, 0.45f, 1.0f, 0.8f),
            Energy = 1.2f,
            Texture = GD.Load<Texture2D>("res://Assets/textures/light_radial.png"),
            TextureScale = 1.4f
        };
        AddChild(_spectralLight);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        float time = (float)Time.GetTicksMsec() / 1000.0f;
        // Ghostly alpha flicker
        Modulate = new Color(1.0f, 1.0f, 1.0f, 0.55f + Mathf.Sin(time * 3.0f) * 0.25f);
        if (_spectralLight != null)
        {
            _spectralLight.Energy = 1.1f + Mathf.Sin(time * 4.5f) * 0.4f;
        }
    }

    protected override void OnInteracted(Player player)
    {
        if (player == null) return;

        StoryDirector.Instance?.SetFlag(StoryFlags.FOUND_ARCHIVIST);

        if (!_hasGivenJournal)
        {
            _hasGivenJournal = true;
            StoryDirector.Instance?.SetFlag(StoryFlags.ARCHIVIST_JOURNAL_COLLECTED);

            var journalRes = GD.Load<ItemData>("res://Resources/Items/Archivist_Journal.tres");
            if (journalRes != null)
            {
                player.InventoryComponent?.AddItem(journalRes, 1);
            }

            AudioManager.Instance?.PlaySound(SoundType.ItemUnlock, GlobalPosition);
            HUDUI.Instance?.ShowHealNotification("ARCHIVIST'S CIPHER JOURNAL", 0, "SECRET LORE ACQUIRED");
        }

        base.OnInteracted(player);
    }
}
