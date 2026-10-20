#!/bin/sh
# DISABLED 2026-10-20 after N1a hang: RestartItemPlaceState Dobby froze level load.
# Evidence: docs/evidence/ithm-restartitemplacestate-hang.md
# Safe path: ./run_bepinex.sh (MS 0.4.9 default OFF — N1a/OnBecomeActive diag refused).
echo "ERROR: run_bepinex_diag_restart.sh disabled after RestartItemPlaceState hang (N1a)." >&2
echo "Use ./run_bepinex.sh (MS 0.4.9 diag OFF). Re-enable only after hang rootcause + new knife." >&2
exit 1
