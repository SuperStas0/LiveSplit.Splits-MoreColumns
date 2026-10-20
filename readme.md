<p align="center">
  <img src="Showcase.png" alt="LiveSplit"/>
</p>

# LiveSplit.Splits component with more column types

## Installing
Download `LiveSplit.Splits.dll` file from releases and move it to `C:/Program Files (x86)/LiveSplit/Components` (or wherever you installed livesplit)

## Columns
- **Completed** - shows how many times the split has been completed (skipped splits count as completed)
- **Reached** - shows how many times the split has been reached
- **Passrate** - shows completed / reached
- **Reachrate** - shows the chance to reach the split: the passrates of all splits before it multiplied together
- **Estimated Reaching Time** - Estimated average playtime needed to reach certain split. Calculates based off passrates and mean split times

The running attempt only counts for splits it has already passed, so arriving at a split does not lower its passrate. It counts for the split it is on only once you reset there.

Counts are tracked per split, so reordering, inserting, removing or renaming splits does not break the rates.
### Coloring
For the **passrate** and **reachrate** column types, enable **Color by rate** in the column settings. Color changes from red at 0% to yellow at 50% to green at 100%.

Layouts from earlier versions keep working: setting the column timing method to Game Time also enables coloring.

### Where the counts are stored
Counts are saved in the splits file (`.lss`) as one permanent run variable named `SplitsReachStats`, keyed by split name.
- Splits files without it are recalculated from the segment history when opened, and the variable is written on the next save
- LiveSplit without this component ignores the variable; attempts made that way are added the next time the file is opened with it
- A renamed split is matched again by its segment history; a newly added split starts at 0

## Building from source

Building has same requirements as for [LiveSplit](https://github.com/LiveSplit/LiveSplit):
- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [.NET Framework 4.8.1 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net481-developer-pack-offline-installer) (Which should be preinstalled at most windows installations)

1. Clone repository
2. Replace files in `/lib/` as needed (from LS installation directory)
3. Run `dotnet build`
4. Get `LiveSplit.Splits.dll` from `/artifacts/bin/LiveSplit.Splits/debug/`