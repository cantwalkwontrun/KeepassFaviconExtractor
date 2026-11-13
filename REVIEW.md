---
lang: en-GB
---

# Local normalisation maintenance candidate

This candidate identifies itself as **1.1.5.1**, based on upstream 1.1.5.0 and the current upstream source at `26133d3`. It has not been forked, released or installed by this task.

## Findings and required fixes

- HTML login/challenge responses could reach the raster decoder, producing `Parameter is not valid`. A query string ending in `.svg` could instead select SVG conversion, whose worker wrapper concealed the actual error behind a generic STA message. Synthetic HTML responses reproduced these failure paths; the real website in the reported screenshot was not contacted. The exact remote response was not inspected.
- HTML is now rejected before decoding with an explicit non-image/sign-in message. Raster signatures take precedence over misleading MIME types or extensions. Extension checks use the URL path, and the candidate log shares the normaliser's SVG classification.
- Valid SVG conversion already worked in baseline tests. The existing SharpVectors PNG converter is retained. Conversion errors now preserve their underlying exception and stack, and cancellation is no longer reported as an ordinary failed candidate.
- Untrusted SVG is parsed with DTDs prohibited, no XML resolver and limits on input size, depth, nodes and attribute values. External references, stylesheet instructions/imports, CSS escapes, embedded raster images and active content are rejected. Ordinary local paint/shape references remain supported. Cycles and excessive expansion through fragment-href instancing are rejected before rendering; CSS resource recursion is not covered by that graph check.
- SharpVectors external-resource access is explicitly denied, and proportional raster dimensions are capped at 1024 pixels before conversion. This avoids allocating an unrestricted source-sized raster before resizing it to the normal 128-pixel icon.
- Only one renderer can run at once. Timeouts include waiting for its slot, and the slot remains occupied until the actual renderer exits. Late disposable results are released, including after cancellation. STA callers receive the same timeout policy as other callers; unsafe thread abortion is not used.

These changes are required for clear failure handling, cancellation/resource correctness and the untrusted SVG boundary. They do not change favicon providers or database assignment policy.

## Verification and review

The official KeePass 2.61 reference archive was checked against the repository's pinned SHA-256 before extraction. The candidate builds against that reference and .NET Framework 4.8. No new KeePass APIs or runtime packages were introduced.

The original square and rectangular SVG tests passed before changes. Two added HTML regression cases failed against the original normaliser. The final suite contains **59 synthetic checks**, all passing on both x64 and x86 test hosts. They include normal PNG/ICO/WebP/SVG paths, HTML and misleading hints, prohibited references/DTD, deep and expanding SVG graphs, large declared dimensions, real STA execution, deadlines, cancellation, retained-slot concurrency and late-result disposal. Test results are recorded locally; no real password database was opened or saved.

Three separate source reviewers examined security/resource boundaries, robustness/lifetime and diagnostics/compatibility. Their findings were implemented and rechecked. The implementing developer ran the tests; reviewers did not independently execute them. One existing unreachable-code warning remains in `WebpDecoder.cs`.

## Limits

The precise remote SVG/login response and full interactive KeePass workflow remain untested. This is a targeted maintenance review, not a comprehensive audit or security certification. The safer SVG subset can reject icons needing external resources, embedded raster images, escaped CSS/references or excessive complexity; ordinary fallback candidates can still be tried. Recursive CSS pattern/marker/mask behaviour remains unverified against the renderer.

A permanently stuck renderer cannot safely be terminated in-process. It retains the single slot until KeePass exits, causing subsequent SVG attempts to time out rather than accumulating rendering workers. The checks reduce resource exposure but do not provide process isolation or a guarantee against every decoder vulnerability.

The upstream packaging script automatically includes debug symbols and has not been used for this candidate. Local binaries, dependencies, logs, screenshots and installation data remain excluded from Git. The upstream update URL remains unchanged; this local candidate is not represented as an upstream release.
