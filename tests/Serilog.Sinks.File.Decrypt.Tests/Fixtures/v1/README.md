# v1-format fixture files

These files pin the **v1 on-disk format** as produced by the shipping v5.x writer
(`LogWriter`, commit `4c53f7a`, pre-#83). They are the backward-compatibility gate:
the v2-era reader must keep decrypting them byte-for-byte forever.

Do **not** regenerate these with a v6+ writer — the whole point is that they were
written by the last v1-emitting release. If they ever need to be regenerated,
check out the `5.x` release tag and use `LogWriter` from there.

| File | Contents |
|---|---|
| `single-session.log` | One session, 5 messages, empty keyId |
| `multi-session.log` | Two independent sessions appended to one file (3 + 4 messages), empty keyId |
| `with-keyid.log` | One session, 3 messages, keyId `fixture-key` |
| `*.expected.txt` | Exact expected plaintext output of decrypting the matching `.log` |
| `fixture-private-key.pem` / `fixture-public-key.pem` | Dedicated throwaway 2048-bit RSA key pair used only for these fixtures. **Not a secret** — never use it for anything else. |

All fixtures were verified to decrypt with the v5.x reader in
`ErrorHandlingMode.ThrowException` before being committed.
