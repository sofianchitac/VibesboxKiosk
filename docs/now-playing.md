# Now Playing feed

The kiosk can show the current track over the spectrum. It connects as a
WebSocket **client** to the URL set in *Settings → Now Playing* and reconnects
on its own (2 s, doubling up to 30 s). Each text frame is one JSON object.
Unknown fields are ignored, and so is any frame without `"schema": 1`.

## Events

### `track_change` — a new track, or new metadata for the current one

```json
{
  "schema": 1,
  "event": "track_change",
  "producer": "airplay",
  "playing": true,
  "playback_rate": 1.0,
  "source_app": { "display_name": "AirPlay" },
  "track": {
    "unique_id": "a1b2c3",
    "title": "Song",
    "artist": "Artist",
    "album": "Album",
    "duration_seconds": 215.0,
    "elapsed_seconds": 12.5
  },
  "artwork": { "mime": "image/jpeg", "data_base64": "…", "sha256": "…" }
}
```

- Only `title` is required. The card chooses its layout from what arrives: the
  title alone, text only, or artwork beside the text.
- The card pops up when `unique_id` changes. Keep the id stable while a track
  plays.
- When `duration_seconds` and `elapsed_seconds` are both set, a progress bar
  runs. The kiosk moves it forward on its own clock at `playback_rate`, so you
  don't need to send position updates.
- `producer` is a free-form id. *Secondary-colour producers* in the settings
  draw the card in the secondary colour instead of the accent.

### `state_change` — play/pause or seek

```json
{ "schema": 1, "event": "state_change", "playing": false, "playback_rate": 0.0, "elapsed_seconds": 80.2 }
```

### `cleared` — nothing is playing

```json
{ "schema": 1, "event": "cleared" }
```

On connect, send the current state right away so the card is correct after a
kiosk restart.
