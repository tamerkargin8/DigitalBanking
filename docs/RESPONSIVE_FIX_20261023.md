# Responsive layout correction — 23 October 2026

The Login Bootstrap column was nested inside a shrink-to-fit grid item, producing a 154px card in an 847px browser. Replaced that wrapper with a fluid, full-width auth container capped at 640px for readability. The auth grid now explicitly uses minmax(0, 1fr), adaptive padding and dynamic viewport height. Footer links wrap. Removed the 1380px content cap from authenticated pages so Owner/customer dashboards use available browser width. No authentication or financial logic changed.

Independent browser checks: Login at 320, 390, 768, 1024 and 1440px had document width equal to viewport width (no horizontal overflow); card widths were 288, 358, 640, 640 and 640px. At the actual 847px panel, card width changed from 154 to 640px. Browser viewport override reset after testing.

Full solution and UAT builds: zero warnings/errors. UAT source/database backed up; every table row remained identical after restart. Manual UAT remains pending.
