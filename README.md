# Even Faster Boss Wait: Tokyo Drift
> "Void reavers that appear at the end, literally what is their purpose. I can just hide in the spaceship and wait for the ending to trigger lmfao what"

This is a rewrite/continuation of [FasterBossWait by ChrisStevensonGit](https://thunderstore.io/package/ChrisStevensonGit/FasterBossWait/), made to be highly configurable. 

By default, when you kill an enemy after the boss has been defeated, every kill will reduce the teleporter duration by a second.

## Features
- Enhanced FasterBossWait features
    - Whether saved time should be re-added to run timer
    - Whether to use % values or seconds
    - Bonus charge given per HP, Elite status, [Variant](https://thunderstore.io/package/Nebby/VarianceAPI/) status, Miniboss/Champion status
    - Reduction before the boss is killed / outside the teleporter range
    - Holdout Zone Time Multiplier per person, stage count and loop count
    - Holdout Zone Area Multiplier per person
    - Configurable time/area per holdout zone
    - Kills effect multiplier per holdout zone
    - General Multiplier (person, stage, loop) effect multiplier per holdout zone
    - Charge rate increase after boss kill per holdout zone
    - Area increase after boss kill per holdout zone
- Extra Holdout Related Tweaks
    - Focused Convergence rate/range limit (vanilla limits both to 3 stacks)
    - Unlock interactables (after boss kill)
    - Unlock interactables inside void seeds
    - Time Pauses when every player is near the charged teleporter (radius configurable)

## Changelog
- 1.1.8: fixed a lot of the math
- 1.1.7: fixed multiplier per loop, focused convergence tweaks, non-teleporter non-boss check
- 1.1.6: made to work in SotS, fixed bosskill multiplier being applied without killing the boss
- 1.1.5: WRB compat, fixed log spam with teleporter
- 1.1.4: VariantAPI compat, charged time pause radius
- 1.1.3: Bugfix
- 1.1.2: Fixed logspam on simulacrum
- 1.1.1: Bugfix, Kill Compensation General Scaling Multiplier
- 1.1.0: Added Kill Compensation (per Smxrez's request)
- 1.0.0: Initial Commit