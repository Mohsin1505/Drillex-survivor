// Anything the HUD's circular ability button can drive. The button finds the equipped
// implementation on the train at runtime, so new powers only need to implement this.
public interface IHudAbility
{
    // Stable key used to pick the button's icon and colour (e.g. "soundtrap", "fire", "electric", "missile").
    string AbilityId { get; }
    string DisplayName { get; }

    // 0 = just used, 1 = fully recharged.
    float CooldownProgress { get; }
    float CooldownRemaining { get; }

    // True while the ability's effect is still alive in the world (e.g. a deployed trap).
    bool IsActive { get; }

    bool CanActivate { get; }
    void Activate();
}
