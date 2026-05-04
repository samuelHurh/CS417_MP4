namespace JerryScripts.Foundation.Damage
{
    /// <summary>
    /// Rarity tier of a weapon. Drives:
    /// <list type="bullet">
    ///   <item>Damage multiplier via <see cref="RarityMultiplierTable"/></item>
    ///   <item>HUD-06 rarity-name color (HUDSystem.GetRarityColor)</item>
    /// </list>
    ///
    /// <para>Originally declared in <c>Feature/WeaponHandling/WeaponData.cs</c>; extracted
    /// here so the enum survives deletion of the deprecated weapon-handling system after
    /// Sam's BNG integration.</para>
    /// </summary>
    /// <remarks>Sprint Final, Phase 4 prep. Replaces <c>JerryScripts.Feature.WeaponHandling.WeaponRarity</c>.</remarks>
    public enum WeaponRarity
    {
        /// <summary>Default tier. Multiplier 1.0×. HUD color: warm off-white.</summary>
        Basic = 0,

        /// <summary>Multiplier 1.3×. HUD color: sky blue.</summary>
        Rare = 1,

        /// <summary>Multiplier 1.7×. HUD color: purple.</summary>
        Epic = 2,

        /// <summary>Multiplier 2.2×. HUD color: gold.</summary>
        Legendary = 3
    }
}
