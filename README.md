Org Cap from Councilor Level

**Requires the Councilor Level mod**

Changes the org cap to depend on councilor level, which increments every time you apply an augmentation.

This has the effect of greatly diminishing the value of the Administration attribute, which has been
accordingly reflected in the AI prioritization.

Each level grants you one more org cap; each org tier consumes one org cap. All councilors start at
level 1 with 1 available org cap. For instance, to add a Tier 3 org to a councilor, they must be at 
least level 3 with no attached orgs. 

NOTE: Currently, localization files (which control text output) are not able to be modded easily.
Therefore the Councilor Info window still shows administration as linked to org capacity - this will
be fixed when 0.3.24 is live.

Integration Notes:

Administration is widely used throughout the TI codebase. This unfortunately means that 
a couple of methods had to be completely bypassed in this mod:

TICouncilorState.CanRemoveOrg_Admin - is always true now, because there is no effect of removing
an assigned org on total available org cap.

TIFactionState.ValidateAllOrgs - this method previously checked to see if an org should no longer
be assigned to a councilor because something about that councilor changed since the last check -
one of those possible changes being a decrement of Administration trait. Since the administration
trait is not what determines org capacity & the Administration check is not encapsulated, this 
entire method must be bypassed. 

AIEvaluators.EvaluateOrgForCouncilor - Same as above - the AI checks the `availableAdministration`
attribute directly rather than calling an encapsulated method, so this entire method must be bypassed.


Changelog:

October 14 2022 - Initial Upload