Recorded voice lines (ElevenLabs mp3s). Each file is named by its id in the team's voice_lines.csv,
e.g. dexter_bg_01.mp3, and plays instead of the placeholder babble for that line.

Where a csv id differs from the game's line id (dexter_step1_excuse is the line "dexter_excuse"), the
mapping is at the end of Scripts/Story/Lines.cs ("recorded clips named differently from their line").
Brand-new lines (new background chatter, new guide lines) need an L(...) entry in Lines.cs too.

Lines still using babble are listed in voice_lines_todo.csv at the project root, in the same csv format.
Import settings: mono, Vorbis, compressed in memory (spatialised at each kid's head on Quest).
