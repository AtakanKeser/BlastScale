-- Found by the first load test: "UPDATE game_session ... WHERE player_id = ? AND status = 'ACTIVE'"
-- (abandoning a player's open session) could be served from the (status, started_at) index,
-- which made InnoDB lock a range spanning *other players'* ACTIVE rows and deadlock with their
-- concurrent inserts. A (player_id, status) index makes the statement touch one player only.
ALTER TABLE game_session ADD KEY ix_session_player_status (player_id, status);
