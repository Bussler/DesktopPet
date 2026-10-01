# Ticket 06: sleep and Windows resume manual acceptance

Run Hood on Windows with the default settings.

- Open Hood's right-click menu and choose **Sleep**. Confirm the dedicated sleep animation loops and the sleep need decreases while hunger and boredom stay steady.
- Left-click Hood while sleeping. Confirm he wakes and plays the wake animation without also jumping; automatic sleep should wait through the configured grace period.
- Start another nap, choose **Feed**, and confirm Hood wakes, plays the feed animation, and hunger falls.
- Reach the automatic sleep threshold during a jump or feed animation and confirm sleep begins after that action finishes.
- Put Windows to sleep while Hood is open, wait several minutes, then resume. Confirm the nap/awake timer and needs did not advance by the time Windows was suspended.
- Quit while a nap or grace period is in progress, relaunch, and confirm the remaining active duration resumes unchanged.
