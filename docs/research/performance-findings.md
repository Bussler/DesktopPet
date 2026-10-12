# Performance findings: RAM and Notes latency

> Historical investigation from 2026-10-08, with an integration note from 2026-10-09. Measurements and acceptance status below describe those candidates; they have not been rerun for the current checkout. See [project overview](../project.md) and [build workflow](../build.md) for current development guidance.

Investigation date: **2026-10-08**. Source snapshot: `067a6074c4924c5c716d382d3e5693f3b0472b5d`.

This document consolidates the investigations into approximately 200 MB of reported RAM usage and sluggish typing/formatting in Notes. It records the original measurements, the ten performance changes implemented in this worktree, integrated Release results, and verification still required. Measurements describe these fixtures and this machine; they are not universal release limits or native frame-rate guarantees.

Integration update, **2026-10-09**: the implemented changes were rebased onto `main` at `01046e5f8516fd4d875c0c73028da4b6c0260017`, preserving its newer pet-positioning and nested-list behavior. The complete validation workflow passed again: locked restore, sprite validators, and 888 Release tests (374 Core, 514 Windows), with zero failures or skips. The measurements and package hashes below remain historical evidence for the pre-rebase candidate; performance probes and native acceptance have not been repeated on the rebased candidate.

## Initial diagnosis at a glance

| Area | Finding | Measured opportunity |
| --- | --- | --- |
| Baseline RAM | The full decoded sprite atlas occupies 97.5 MiB, despite its PNG being only 15.52 MiB | A smaller decode reduced the isolated pet workload to approximately 142–144 MiB total working set |
| Animation allocation | Different frame indices frequently reference the same atlas rectangle | Suppressing redundant crops reduced 60-second private commit from 465.94 to 247.75 MiB; resident savings were much smaller |
| Multiple Notes | Common theme/style resources are instantiated repeatedly | Sharing styles for ten blank Notes reduced managed heap from 47.04 to 27.24 MiB |
| Hidden sprites / previews | Hidden mode retains the decoded sheet; Settings retains the original sheet for rollback | Avoid or reduce these decodes while preserving reveal and Cancel behavior |
| Formatting latency | Styling a small selection rebuilds the whole editor document | With real saving enabled, a local-formatting prototype reduced median latency from 40.70 to 7.79 ms for 100 paragraphs |
| Typing latency | Whole-document scans, Markdown passes, history accounting, saving, and list refreshes run during edits | Reduce repeated work and limit processing to the changed content |

## Measurement scope and interpretation

Both investigations used temporary Release probes referencing the actual app projects, running .NET / Windows Desktop **10.0.12 x64** with SDK **10.0.401**. Production startup was suppressed. Probes used synthetic notes and local diagnostic files, without loading or changing personal notes, configuration, or startup registration.

The RAM probe exercised the real asset loader, pet renderer, Notes creation/hiding/closing, and Settings preview/Cancel callbacks. It had no tray icon. No DesktopPet process was running when the initial inspection began, so these reproduce workloads rather than measure the user's exact session or Task Manager counter.

The latency probe exercised the real Notes editor, model, JSON store, and optionally Notes list. Its note window was shown off-screen at 440 × 500 with default appearance. The pet overlay was absent. Controlled latency runs disabled tiered compilation in the probe to avoid JIT tier transitions; the product configuration was unchanged. The recorded processor was an AMD Ryzen 5 5600X with 6 cores / 12 logical processors.

Interpret the counters separately:

- **Total working set:** resident process pages, including shared code pages.
- **Private working set:** resident pages that are not shareable.
- **Private commit:** committed private memory, which need not all be resident.
- **Managed heap:** managed objects at the sample boundary; this excludes native bitmap storage and GPU resources. Some RAM samples explicitly forced GC to distinguish retained objects from garbage.
- **Per-edit allocation:** bytes allocated on the UI thread during an edit, not retained RAM. This excludes other threads and native allocations.

Memory values use **MiB = 1,048,576 bytes**. These counters overlap and must not be added together. Forced GC was a diagnostic technique, not a proposed production optimization.

Latency values cover processing through the Dispatcher and layout queue. They are **not measured on-screen FPS, GPU render time, or physical keyboard input-to-pixel latency**. At 60 Hz a frame lasts 16.67 ms; this is a useful reference, not an agreed product requirement. The pet's 33 ms animation timer does not impose a 30 FPS limit on Notes.

## Integrated Release results after implementation

Ten implementation tickets are complete in the candidate worktree: DPI-sized sprite decoding, local inline formatting, unchanged-crop suppression, shared Notes appearance resources, targeted typing recognition, cheaper toolbar/selection work, coalesced Notes-list updates, incremental history totals, releasing decoded sprites while hidden, and compressed rollback retention for Settings previews. Focused regressions and paired probes for each change are recorded in `.codex/performance-ticket-01-20261008.md` through `.codex/performance-ticket-10-20261008.md`.

The integrated matrix compared the pre-change Release assemblies with the final candidate in three sequential fresh-process pairs per workload. Both sides used the same `MemoryProbe.dll` (SHA-256 `45E6AEC920FB70615EB1FFE4BC0EC4663B0DFB563DC4C2992C888F668E77396C`), SDK 10.0.401, .NET and Windows Desktop runtime 10.0.12, x64, the same process defaults, 1.25× DPI, and the same Hood manifest and PNG (SHA-256 `9D8377DB9186E6C62C7370FA252A77AF81E9B97B1A3C3C4147442408CDA448AE` and `81327C14B33AA3B17C678BFADE63D7D51B91E4909BD19E50D09F6B7CCBE7E200`). The original Windows/Core assembly hashes were `5A4CF85BB55B7499B0A11818B9AD5CCBC3B7527EC08E3174C324359CA795AB94` and `EFC2A01DBE15852A5EBC84B642E8877BB90EB739B61139F0056936C9FDDCB109`; the final candidate hashes were `1D32CEB171D4C4C6D64699519877A8E8AC83C33CD7B9DA990C6A1A4EF586CE38` and `B343912B214D88AAA8E318703EA36924DD178846BF6BA45D95923D7B64B985A6`. The same diagnostic probe was verified against both builds. Each run used the default runtime tiering settings and no extra warmup. The reported maximum is the maximum of periodic process samples, not a continuous trace.

The measured candidate assemblies are the framework-dependent Release output. The separate self-contained `win-x64` package contains published Windows/Core assemblies with SHA-256 `5B7B5C2FB540D2DD42BF7BD25B9D5D4AD1C2D3D0488459DB95B20971FBBE7D05` and `6A67616D0AB7069B001446706A0297346C95FE377E754C0A840F775C6D01890B`; repeating the publish command reproduced these hashes. The exact package-published assemblies also passed one short diagnostic Notes smoke. The repeated performance comparisons use the same source changes in framework-dependent Release mode; full manual package acceptance remains pending.

| Workload and counter | Original median (range) | Candidate median (range) |
| --- | ---: | ---: |
| 5-second pet: peak total working set | 224.65 MiB (224.04–225.35) | 164.04 MiB (163.86–164.10) |
| 5-second pet: peak private working set | 139.80 MiB (139.18–140.34) | 79.20 MiB (79.04–79.24) |
| 5-second pet: peak private commit | 201.67 MiB (201.02–201.95) | 134.55 MiB (134.22–134.66) |
| 60-second pet + one/ten Notes: peak total working set | 310.84 MiB (310.57–311.45) | 210.88 MiB (210.81–211.32) |
| 60-second pet + one/ten Notes: peak private working set | 211.59 MiB (211.41–212.14) | 111.65 MiB (111.56–111.78) |
| 60-second pet + one/ten Notes: peak private commit | 280.59 MiB (279.45–282.79) | 176.41 MiB (176.12–178.38) |

In the 60-second workload, the one-note sample was taken after its first window opened; the ten-note sample followed after nine more opened. At the ten-note sample, managed heap fell from a median 51.47 to 17.34 MiB, and the probe counted 40 style/theme dictionaries in the original versus one shared dictionary of each kind in the candidate. The notes were then closed, and the pet continued to the 62-second sample. After that cleanup, private working set was 151.08 MiB (150.87–151.20) versus 90.19 MiB (89.84–90.52); managed heap was 4.81 versus 4.56 MiB. This demonstrates release after the tested close/collection boundary, not absence of all future leaks.

The pet remained visible in both builds. Across the three 60-second runs, the median timer count was 1,321 versus 1,316; this is a Dispatcher callback count, not a measured FPS. The probe counted 821 versus 225 image-source changes, 600 versus zero consecutive duplicate crop rectangles, and 1,332.5 versus 696.3 KiB allocated inside frame callbacks. Both builds rendered the same 44 distinct rectangles. The five-second frame counts and elapsed durations were also close. These results support lower image churn without evidence of skipped simulation work; on-screen smoothness still requires native acceptance.

One matched three-minute soak followed the same early one/ten-Note open/close schedule. After Notes closed, original/candidate private working set was 144.86/81.74 MiB at 60 seconds, 143.92/80.78 MiB at 120 seconds, and 143.89/80.78 MiB at 180 seconds; private commit was 215.93/149.25, 215.21/148.22, and 215.23/148.17 MiB at those points. These traces did not show continued growth after the lifecycle burst, but one pair cannot establish a general leak guarantee. Full `tools/Test.ps1` validation passed separately: locked restore, sprite/asset validators, and 827 Release tests (363 Core and 464 Windows), with zero failures or skips. The fast package workflow also passed separately. Its executable is `.scratch/hood-performance/release-candidate-20261008/app/DesktopPet.Windows.exe`, SHA-256 `19BC469D12A40F968E47FD65C363A4C51103B6128AF200E31E9281FBA1AEE0E9`, built from source revision `067a6074c4924c5c716d382d3e5693f3b0472b5d` plus the listed worktree changes. Package allowlist inspection confirmed internal specs and diagnostic files were not included.

Native Windows acceptance remains incomplete. The exact package was launched only with isolated scratch data. Its shell-launched process showed a Windows startup-setting mismatch dialog and exited before interaction. A scratch-only launcher through the supported `@oai/sky` Computer Use API timed out with `Computer Use app approval timed out`, and no Hood window became targetable. A direct launch without `HOOD_DATA_DIRECTORY` was avoided to protect the user's local profile. Required DPI, physical-keyboard, clipboard, visual-smoothness, Notes-rich-content, and Settings workflows therefore remain unverified; ticket 11 is intentionally open. No universal 200 MiB limit or FPS claim is made.

## Original RAM diagnosis (before implementation)

### Representative measurements

Rows come from separate workload probes, not one continuous session. Comparisons at 60 seconds use the same elapsed time. Other rows isolate asset loading, Notes, or previews; the detailed RAM report records their sampling and GC boundaries.

| Scenario | Total working set | Private working set | Private commit | Managed heap |
| --- | ---: | ---: | ---: | ---: |
| WPF app object before assets, collected | 42.03 | 6.71 | 11.91 | 0.33 |
| Real Hood atlas loaded, collected | 149.64 | 105.43 | 110.56 | 0.65 |
| Original pet renderer, 5 seconds, normal allocation | 213.58 | 132.02 | 188.64 | 5.66 |
| Original pet renderer, 60 seconds, collected | 225.60 | 142.88 | 465.94 | 3.13 |
| Half-sized atlas + crop reuse, 5 seconds, collected | 143.15 | 61.05 | 100.75 | 3.16 |
| Original atlas + crop reuse, 60 seconds, collected | 220.59 | 137.85 | 247.75 | 3.14 |
| Pet + one blank Note, collected | 254.61 | 160.09 | 203.67 | 8.83 |
| Pet + ten blank Notes, collected | 291.72 | 193.39 | 244.31 | 47.04 |
| Pet + ten blank Notes, shared-styles prototype, collected | 276.86 | 177.86 | 228.86 | 27.24 |
| Preview Puddle while retaining original Hood, collected | 315.84 | 229.47 | 262.79 | 4.64 |
| Switching preview to Puddle, transient sample | 412.93 | 326.86 | 360.21 | 4.72 |
| After closing Settings and collecting | 211.59 | 125.54 | 160.25 | 3.44 |

All numeric columns are MiB. The baseline pet workload was also reproduced in independent short runs at 214.17 and 224.99 MiB total working set.

### 1. Decoded sprite size dominates the baseline

`HoodAssets.LoadPet` decodes the PNG without requesting a smaller pixel size. Hood, Pip, Crumb, and Puddle use 6144 × 4160 atlases. Hood loads as `Bgra32`:

```text
6144 × 4160 × 4 bytes = 102,236,160 bytes = 97.5 MiB
```

PNG compression reduces disk size, not the decoded pixel buffer. Frames are 384 × 416 source pixels but display at 192 × 208 logical units (`spriteScale: 2`). A probe decoding at 3072 × 2080, with adjusted crop metadata, reduced the pixel buffer to **24.375 MiB**. The roughly **73 MiB** reduction matches the difference in pixel storage. An independent smaller-decode probe without crop reuse reached 141.70 MiB working set.

The pet's collected managed heap was only about 3 MiB while process working set exceeded 200 MiB. Much of the baseline is native image/rendering storage and framework/shared pages; managed-object tuning alone cannot remove the atlas cost.

**Recommendation:** decode to the physical pixel size required by the current DPI, preserving the original source for higher-DPI displays. Estimated atlas buffers are 24.375 MiB at 100% scaling, 38.086 MiB at 125%, 54.844 MiB at 150%, and 97.5 MiB at 200%.

**Compatibility constraint:** setting `DecodePixelWidth` alone is insufficient. Manifest validation and crop calculations currently use the original dimensions. Keep validated source dimensions separate from decoded crop dimensions, or provide matching image/manifest variants. Verify integral crop boundaries, visual quality, DPI transitions, animation state, and custom sprite compatibility.

### 2. Repeated atlas rectangles cause redundant bitmap work

The renderer skips a frame only when action and frame index are unchanged. Idle has 136 manifest entries but only 16 distinct cells, so changing the index often recreates a frozen `CroppedBitmap` and assigns `Image.Source` for identical pixels.

At 60 seconds the original probe had **797 source assignments**, including **569 consecutive duplicate rectangles**. A rectangle-reuse prototype had **229 assignments** and **zero consecutive duplicates**, with comparable animation progression. Private commit fell by 218.19 MiB, while total working set fell by only 5.01 MiB.

**Recommendation:** compare the displayed rectangle and asset identity before creating a crop. Pet switching must invalidate the comparison; simulation, positioning, and elapsed-time updates must continue. This is smaller than introducing a cache of every frame.

The one-minute commit growth does not prove an indefinite leak or identify its native allocator. A longer soak and native allocation trace are needed to characterize reclamation and any remaining growth.

### 3. Notes duplicate common style resources

`NotesAppearance.Apply` loads common Notes styles and Fluent resources for each surface, including multiple menu surfaces. A blank editor therefore carries more than its text/document state.

Sharing the common stylesheet by resolved theme replaced **30 duplicate dictionaries** across ten blank Notes. Managed heap fell from **47.04 to 27.24 MiB**, and private working set from **193.39 to 177.86 MiB**. The experiment covered the current system theme and blank Notes, not all appearance combinations.

**Recommendation:** own reusable styles/templates at the application resource layer, with appropriate theme variants. Keep per-note colors, mutable preview state, content, and controls local. Verify dynamic resources, all UI styles, system theme changes, and preview rollback.

This is comparable to the **flyweight pattern**: common styles/templates/theme resources are shared intrinsic state; note content, color choices, selection, geometry, and preview state are extrinsic. WPF resource sharing provides the mechanism without requiring a new flyweight class hierarchy. Live editor/control instances should retain their normal ownership.

Lazy construction of optional context menus is an additional candidate; its benefit was not separately measured.

### 4. Hidden mode and previews retain decoded assets

Hidden startup still decodes the selected atlas. A hidden-sprite probe reached **182.85 MiB working set** without showing the overlay. Hiding stops animation but retains the sheet.

Settings intentionally keeps `_originalAssets` for Cancel. Previewing another pet retains two full sheets, and a third can exist transiently during replacement. The measured preview reached **412.93 MiB**, then returned to approximately **211.59 MiB** after closing Settings and collection. This scenario did not demonstrate permanent retention of unused pets. Startup loads the selected pet rather than all four.

**Recommendations:** load sprite pixels when they are needed for display; retain metadata/session state for hidden operation. To release an already decoded atlas, clear both its asset reference and `Image.Source`, because a crop retains its source. DPI-sized decoding also reduces preview costs. If further savings are needed, retain original compressed bytes and metadata for rollback rather than a second decoded sheet; using only a file path risks failed rollback if assets change or disappear.

Group-hidden Notes intentionally keep their editors for quick reopening. Ten hidden Notes still retained 47.04 MiB of managed objects; after closing and deferred cleanup/collection this fell to 4.83 MiB. Releasing long-hidden editors is a later option, with reopening cost and caret/scroll/history preservation requirements. Ordinary closing already removes the windows.

### 5. Demand-loaded animation assets are a larger follow-up

A full-resolution 16-frame state strip contains about **9.75 MiB** of pixels. Prepacked strips or a bounded cache of independently decoded frames could reduce steady bitmap storage while retaining DPI quality.

This is an estimate, not a measured implementation. It adds asset-generation and transition/error-handling work and must preserve the sprite-sheet replacement contract. Cropping the existing full atlas does not release that atlas; repeatedly decoding the full PNG could increase CPU and transient memory.

## Implemented Notes latency results

### Results from the implemented Notes changes

The following Release probes measured targeted changes in sequence. Each row uses the baseline for that ticket, so values are not one end-to-end comparison against a single unchanged build. Latency includes WPF editor work and, where stated, the Dispatcher/layout drain. None of these measurements represents physical key-to-pixel latency or on-screen FPS.

| Targeted workflow | Fixture | Median baseline → candidate | p95 baseline → candidate | Managed allocation baseline → candidate |
| --- | --- | ---: | ---: | ---: |
| Bold formatting with real JSON save | 10 paragraphs | 11.45 → 3.64 ms | 15.54 → 4.01 ms | 893.5 → 318.5 KiB |
| Bold formatting with real JSON save | 100 paragraphs | 36.58 → 7.17 ms | 50.21 → 9.05 ms | 4,578.9 → 1,699.6 KiB |
| Bold formatting with real JSON save | 500 paragraphs | 152.36 → 22.90 ms | 174.53 → 39.71 ms | 19,771.0 → 7,877.2 KiB |
| Typing with an unrelated old marker elsewhere | 500 paragraphs | 33.70 → 19.32 ms | Not reported | 14.80 → 3.40 MiB |
| Caret move through 500-paragraph editor | 500 paragraphs | 4.66 → 2.99 ms | 6.18 → 4.28 ms | 839.9 → 284.5 KiB |
| Typing with 25 saved notes and visible list | 25 notes | 14.52 → 7.75 ms | 25.77 → 16.20 ms | 2,723.68 → 1,684.30 KiB |
| History accounting at the 100-entry cap | 100-paragraph model | 1.87 → 0.10 ms | 2.65 → 0.11 ms | 2,686.76 → 191.12 KiB |

The integrated ticket 11 matrix additionally measured typing with a real JSON save on every edit, using three fresh-process original/candidate pairs at each paragraph size:

| Paragraphs | Median edit latency (process-median range), original → candidate | Median process p95, original → candidate (range) | Highest observed per-process maximum | Median UI-thread allocation, original → candidate (range) | Saves per process and fixture |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 10 | 3.55 ms (3.40–3.56) → 3.33 ms (3.30–3.38) | 4.24 (4.01–4.35) → 4.39 (4.19–5.25) ms | 4.76 → 16.16 ms | 253.1 (252.6–254.0) → 161.0 (160.8–161.0) KiB | 30 → 30 |
| 100 | 7.89 ms (7.16–8.06) → 5.39 ms (5.31–5.53) | 14.73 (14.30–15.65) → 7.35 (6.80–12.49) ms | 20.07 → 21.22 ms | 1,616.2 (1,615.3–1,617.5) → 731.1 (731.0–731.1) KiB | 30 → 30 |
| 500 | 26.19 ms (26.00–27.48) → 13.06 ms (12.77–15.19) | 35.33 (34.46–37.31) → 20.55 (20.36–23.74) ms | 37.47 → 27.71 ms | 7,665.4 (7,664.7–7,665.7) → 3,232.9 (3,232.3–3,233.1) KiB | 30 → 30 |

The 10-paragraph timing difference is within process variation; allocation fell. At 100 and 500 paragraphs, process-median and p95 ranges were separated. One candidate 10-paragraph run had a 16.16 ms isolated maximum, and the 100-paragraph candidate also had a slightly larger single maximum (21.22 versus 20.07 ms) despite its lower median and p95. At 500 paragraphs, original processes had 30/30 edits above 16.67 ms; candidate processes had 4–6/30 (median 5). The candidate is substantially cheaper, but this timing is still not on-screen FPS. Marker insertion/deletion, headings, pending/completed URLs, and literal code paste have deterministic regression coverage. The ticket 07 visible-list change eliminates active/Trash searches on ordinary edits; the one-note hidden-list timing was within variation while allocations fell. These results preserve distinct durability coverage: ticket 02 and ticket 11 used real JSON saves, while tickets 05–06 isolated editor work with persistence disconnected; the full Release suite covers saves, retries, and recovery.

The 500-paragraph formatting median remains above a 16.67 ms 60 Hz frame interval even after the change. That interval is context, not a product threshold; the native manual check is still required to assess visible smoothness.

## Original Notes latency diagnosis (before implementation)

### Typing and current formatting

The fixtures contain 10, 100, or 500 paragraphs with approximately 918, 9,198, or 45,998 characters. The literal-marker variant places one underscore in the first paragraph while typing occurs at the end. “25 notes” means one edited/open note and 24 other saved notes of the same initial size.

| Scenario | Median latency | p95 latency | Median UI-thread allocation |
| --- | ---: | ---: | ---: |
| Editor only, plain typing, 10 paragraphs | 1.79 ms | 11.81 ms | 0.16 MiB |
| Editor only, plain typing, 100 paragraphs | 4.75 ms | 16.58 ms | 0.75 MiB |
| Editor only, literal underscore elsewhere, 100 paragraphs | 7.84 ms | 12.26 ms | 3.04 MiB |
| Editor only, plain typing, 500 paragraphs | 23.03 ms | 32.83 ms | 3.40 MiB |
| Editor only, literal underscore elsewhere, 500 paragraphs | 34.02 ms | 42.25 ms | 14.80 MiB |
| Actual model, no disk writes, 100 paragraphs | 5.36 ms | 12.36 ms | 1.49 MiB |
| Actual model + JSON save, 100 paragraphs, 1 note | 8.04 ms | 15.35 ms | 1.58 MiB |
| Actual model + JSON save, 100 paragraphs, 25 notes | 15.15 ms | 24.87 ms | 5.85 MiB |
| Same 25 notes with Notes list shown | 32.69 ms | 44.43 ms | 9.08 MiB |
| Editor only, bold a small selection, 100 paragraphs | 39.62 ms | 46.21 ms | 3.62 MiB |
| Editor only, bold a small selection, 500 paragraphs | 161.57 ms | 180.69 ms | 15.28 MiB |
| Editor only, complete `**word**`, 100 paragraphs | 52.35 ms | 57.78 ms | 7.17 MiB |
| Editor only, complete `**word**`, 500 paragraphs | 198.86 ms | 214.46 ms | 29.46 MiB |

“Editor only” disconnects application persistence while keeping editor processing. Every measured typing operation with the actual model produced one successful store call. Ordinary typing did not replace the document. Completing Markdown produced two update callbacks and one whole-document replacement per measured operation.

### 1. Local formatting avoids the largest measured cost

The current selected-text formatting path reads the document, calculates offsets, builds an updated model, replaces the entire `FlowDocument`, restores selection/scroll with forced layout, saves, and updates the toolbar. Unchanged blocks and embedded renderers are recreated.

A diagnostic prototype applied bold to the existing selection, read and committed the resulting semantic document through the same update delegate, and updated the accepted snapshot and toolbar. With real JSON saving:

| Paragraphs | Current median / p95 | Prototype median / p95 | Current / prototype allocation | Document replacements |
| --- | ---: | ---: | ---: | ---: |
| 100 | 40.70 / 53.03 ms | 7.79 / 23.49 ms | 4.43 / 1.37 MiB | 30 / 0 |
| 500 | 174.72 / 241.29 ms | 22.38 / 35.78 ms | 19.31 / 6.29 MiB | 30 / 0 |

Both variants performed 30 successful JSON saves. The measured improvement was approximately **5.2× / 7.8×**, with roughly two thirds fewer managed allocations. This is not an FPS multiplier. Even the improved 500-paragraph case exceeds a 16.67 ms frame interval.

**Recommendation:** format ordinary selected runs in place, committing once. Reserve wider replacement for structural changes and aim to replace only affected blocks. The prototype checked plain-text selections and semantic bold state; mixed content, code, math, links, tables/lists, undo through the window, viewport behavior, and save failures remain to be verified.

### 2. Existing punctuation triggers whole-note Markdown parsing

`OnTextChanged` schedules the conversion pipeline when the note contains any marker or URL, regardless of where the new edit occurs. A literal underscore far from the caret is sufficient.

An isolated Markdown pass that converted nothing took **2.29 ms and 2.25 MiB** for 100 paragraphs, and **12.21 ms and 11.24 MiB** for 500 paragraphs. The pipeline repeatedly constructs whole-note text and scans formatting candidates.

**Recommendation:** recognize syntax in the changed region and relevant neighbouring blocks, selecting the recognizers that can apply. Preserve wider handling for paste and structural edits, marker deletion, pending URLs, and literal code. Reuse text/offset calculations within each pass. Skipping all recognition for alphabetic input would miss cases such as URL completion.

`Dispatcher.BeginInvoke` safely defers work past the active WPF edit transaction, but its callback still runs on the UI thread. Deferral alone does not eliminate processing cost.

### 3. Toolbar and offset updates scan unrelated content

`UpdateToolbar` serializes the whole editor to determine list state. `GetSelectionOffsets` constructs plain text and walks document prefixes for both endpoints. Internal reconstruction can also trigger selection events and repeat this work.

| Synchronous component | 100 paragraphs | 500 paragraphs |
| --- | ---: | ---: |
| Read editor document | 0.24 ms | 1.34 ms |
| Compute selection offsets | 0.67 ms | 3.35 ms |
| Update toolbar | 0.99 ms | 5.15 ms |
| Rebuild editor document | 9.22 ms | 43.50 ms |

These component times exclude the subsequent Dispatcher drain and overlap internally; do not add them or compare them directly with full-cycle timings.

**Recommendation:** derive toolbar state from the selection and containing blocks, and coalesce internal selection updates into one final refresh. Preserve the stored-text offset contract: WPF `TextRange.Text` adds synthetic list markers and embedded-control spaces.

### 4. Every edit saves and refreshes the Notes list

The model copies and validates the Notes collection at update/save boundaries, then serializes and replaces the JSON file synchronously. The app also refreshes an existing Notes list after every edit; `Refresh` resets both active and Trash ItemsSource collections and reconstructs previews.

With one 100-paragraph note, the median update delegate took **1.99 ms**, including **1.34 ms** inside the JSON store. With 25 notes it took **7.71 ms**, including **4.73 ms** in the store. Showing the list increased full-cycle typing latency from **15.15 to 32.69 ms**, including further list binding/layout work.

Store timing combines validation, JSON serialization, and file I/O; it is not pure disk latency. Update timing includes store and list work, so these figures must not be added.

**Recommendations:** update the changed list preview or coalesce content refreshes, avoiding work for an invisible list while preserving search, selection, membership, and Trash behavior. Move owned-snapshot serialization and file I/O to one ordered writer, keeping live model/editor ownership on the Dispatcher. Maintain atomic replacement, pending/error feedback, and a flush of the latest version before close or destructive operations.

Coalescing saves changes how long accepted edits can remain only in memory; decide that durability policy explicitly. Independent concurrent saves risk stale snapshots overwriting newer edits. Reducing model copies also requires preserving mutable-data ownership and transaction/error behavior.

### 5. Undo accounting reconstructs historical text repeatedly

Each undo/redo stack is capped at 100 snapshots and two million plain-text characters. `PushHistory` repeatedly calculates the character budget by building plain text from stored history documents.

A model-only comparison started with zero history versus 90 entries. Over the measured samples, history grew to roughly 6–35 entries versus 96–100. Median update time was **0.48 vs 1.96 ms**, with **0.68 vs 2.62 MiB** allocated. This includes validation/copies/history work and does not isolate history accounting alone.

**Recommendation:** record entry character counts and maintain a running total. Preserve limits, undo/redo semantics, and snapshot ownership. The character limit is not a byte budget, and histories have no aggregate budget across all notes; rich structures can cost more than their text length suggests.

## Recommended sequence and acceptance

| Priority | Change | Main benefit | Required verification |
| --- | --- | --- | --- |
| 1, RAM | DPI-sized sprite decoding | Largest measured baseline resident-memory saving | DPI quality/transitions, crop boundaries, manifests/custom assets, unchanged logical size/state |
| 1, latency | Local inline formatting | Largest measured formatting-latency improvement | Mixed styles/content, persistence/errors, undo/redo, caret/viewport |
| 2 | Reuse unchanged sprite rectangle | Fewer allocations/source changes and lower private commit | Asset switching invalidation, movement/timing continuity, long soak |
| 2 | Share common Notes resources | Lower retained memory with multiple editors | Theme/style isolation, dynamic resources, colors, preview rollback |
| 3 | Limit Markdown and toolbar work | Less per-keystroke CPU/allocation | Typed syntax boundaries, paste, deletion, URLs, stored offsets |
| 3 | Coalesce list refresh and cache history counts | Smaller scoped improvements during editing | Search/selection/Trash, undo/redo and exact history budgets |
| 4 | Ordered background saving | UI responsiveness as stored content grows | Latest-version ordering, atomicity, failures/retry, close/destructive flush |
| 4 | Reduce hidden/preview asset retention | Lower mode-specific memory peaks | Hidden-startup ownership, reveal continuity/errors, coherent Cancel |
| Later | Demand-loaded animation assets / release long-hidden editors | Potential larger reductions | Asset contract, transition cost, editor state restoration |

Preserve deferred document replacement/recovery in `TextChanged`: WPF is still inside its edit transaction. Preserve selection and viewport around remaining structural replacement. Existing scroll restoration also runs at `DispatcherPriority.Loaded`; local edits can avoid needing it, while structural edits still need its behavior.

After implementing a scoped change, run the relevant deterministic regressions and repeat the corresponding probe with matching counters, elapsed times, fixtures, and runtime settings. Native acceptance remains separate: test physical typing/frame presentation, mixed Notes content, actual clipboard/shortcuts, 100/125/150/200% scaling and monitor crossings, and longer idle/roaming/preview/open-close sessions.

The original investigation below records the pre-change baseline and candidate prototypes that justified the tickets. Since then, tickets 01–10 have been implemented and verified with focused regressions and paired Release probes; the integrated results above summarize the complete candidate. Probe builds succeeded with NU1900 warnings because NuGet vulnerability metadata was inaccessible. Do not introduce forced GC as a workaround for the measured costs.

## Evidence and reproduction

The session bundles are local and ignored by Git. They preserve detailed reports, raw data, and probe sources; the essential findings above remain readable without them.

- RAM investigation bundle (`.codex/memory-diagnosis-20261008/REPORT.md`): `measurements.json`, raw scenario `.jsonl` files, `Program.cs`, and `MemoryProbe.csproj`.
- Notes latency investigation bundle (`.codex/notes-latency-20261008/REPORT.md`): `matrix.json`, `components.json`, `formatting-pair.json`, `environment.json`, and probe source/project/runtime configuration.
- Integrated ticket 11 evidence (`.codex/performance-ticket-11-20261008.md`) and raw paired runs in `.scratch/hood-performance/results-t11/`.

The original RAM probe was run in `C:\github\DesktopPet`; its records are copied into this worktree for reference. Latency measurements were made in this worktree. Both reports identify the same source commit. This consolidated document reuses those measurements; it does not claim a new benchmark run.

To reproduce, place the corresponding probe source/project in `.scratch/memory-diagnosis-20261008` or `.scratch/notes-latency-20261008`, and build with the pinned SDK. The projects reference the actual Windows app:

```powershell
$sdk = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
& $sdk build .\.scratch\memory-diagnosis-20261008\MemoryProbe.csproj -c Release --disable-build-servers -m:1
& $sdk .\.scratch\memory-diagnosis-20261008\bin\Release\net10.0-windows\MemoryProbe.dll $PWD.Path hood original 60
& $sdk .\.scratch\memory-diagnosis-20261008\bin\Release\net10.0-windows\MemoryProbe.dll $PWD.Path hood reuse 60
& $sdk .\.scratch\memory-diagnosis-20261008\bin\Release\net10.0-windows\MemoryProbe.dll $PWD.Path hood half-reuse 5

& $sdk build .\.scratch\notes-latency-20261008\NotesLatencyProbe.csproj -c Release --disable-build-servers -m:1
& $sdk .\.scratch\notes-latency-20261008\bin\Release\net10.0-windows\NotesLatencyProbe.dll .\.scratch\notes-latency-20261008 matrix
& $sdk .\.scratch\notes-latency-20261008\bin\Release\net10.0-windows\NotesLatencyProbe.dll .\.scratch\notes-latency-20261008 components
& $sdk .\.scratch\notes-latency-20261008\bin\Release\net10.0-windows\NotesLatencyProbe.dll .\.scratch\notes-latency-20261008 formatting-pair
```

Additional RAM modes are `hidden`, `preview`, `notes`, and `notes-shared`; the detailed report retains their commands and sampled boundaries. WPF probes require normal Windows access; the restricted sandbox can deny the editor menu's clipboard command-status query. No paste or clipboard overwrite was performed.

RAM probes used operational 150/200 MiB diagnostic markers during development. Those markers are not agreed product limits; raw numbers and the named counters are authoritative. The initial latency probe replaced a selected character repeatedly and did not exercise repeated saving; those results were discarded. The documented latency runs use WPF text composition, assert accepted text length/style, and check save status after each operation.

### Source locations

- [HoodAssets.cs](../../DesktopPet.Windows/HoodAssets.cs): atlas loading/decoding.
- [HoodOverlayWindow.xaml.cs](../../DesktopPet.Windows/HoodOverlayWindow.xaml.cs): timer, crop creation, rendering and visibility.
- [NotesAppearance.cs](../../DesktopPet.Windows/NotesAppearance.cs): stylesheet/theme resources.
- [SettingsWindow.xaml.cs](../../DesktopPet.Windows/SettingsWindow.xaml.cs): original/preview assets and rollback.
- [NoteWindow.xaml.cs](../../DesktopPet.Windows/NoteWindow.xaml.cs): edit/selection events, conversion, formatting, rebuilding, toolbar and offsets.
- [App.xaml.cs](../../DesktopPet.Windows/App.xaml.cs) and [NotesListWindow.xaml.cs](../../DesktopPet.Windows/NotesListWindow.xaml.cs): editor ownership and content-driven list refresh.
- [NotesApplication.cs](../../DesktopPet.Core/NotesApplication.cs): snapshot updates, save flow, undo/redo and history accounting.
- [JsonNotesStore.cs](../../DesktopPet.Core/JsonNotesStore.cs) and [AtomicFile.cs](../../DesktopPet.Core/AtomicFile.cs): JSON validation/serialization and file replacement.

### Framework references

[Microsoft's imaging guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-2d-graphics-and-imaging) supports decoding images at their required display size. [WPF threading](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model) and [layout guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-layout-and-design) explain Dispatcher callback execution and the cost of reconstruction/forced layout. [PSAPI memory counters](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-process_memory_counters_ex) and [working-set flags](https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-psapi_working_set_block) define the process measurements. [WPF BitmapImage source](https://source.dot.net/PresentationCore/System/Windows/Media/Imaging/BitmapImage.cs.html) shows its weak URI-cache references.
