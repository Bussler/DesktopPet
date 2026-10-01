# Ticket 05: needs and Feed manual acceptance

Run Hood and open its pet menu with a right-click.

- Confirm hunger, boredom, and sleep values appear only in the menu, with friendly status text and no permanent desktop bars.
- Leave Hood running while awake and confirm the values rise gradually; relaunch after closing it and confirm closed time added no need.
- Choose **Feed** and confirm hunger drops by about 40 points (or reaches zero) and the dedicated feeding animation plays.
- Click Hood repeatedly during a reaction or feeding animation; confirm each input responds without replaying a long action queue.
- Set `behavior.roaming: false`, let boredom rise, then complete a left-click interaction and confirm boredom drops by the configured stationary-click amount.
- Let needs become high and confirm Hood remains calm, visible, and usable.
