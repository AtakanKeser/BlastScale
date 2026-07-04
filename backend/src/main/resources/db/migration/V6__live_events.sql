-- Live events: time-boxed rule changes configured as JSON.
CREATE TABLE live_event (
    id            BIGINT       NOT NULL AUTO_INCREMENT,
    type          VARCHAR(32)  NOT NULL,
    name          VARCHAR(128) NOT NULL,
    start_at      DATETIME(6)  NOT NULL,
    end_at        DATETIME(6)  NOT NULL,
    configuration JSON         NOT NULL,
    status        VARCHAR(16)  NOT NULL,
    created_at    DATETIME(6)  NOT NULL,
    updated_at    DATETIME(6)  NOT NULL,
    PRIMARY KEY (id),
    KEY ix_event_status_start (status, start_at),
    KEY ix_event_status_end (status, end_at)
) ENGINE = InnoDB;

-- Per-player standing; the (event_id, points, updated_at) index serves ranking queries.
CREATE TABLE live_event_participation (
    event_id     BIGINT      NOT NULL,
    player_id    BIGINT      NOT NULL,
    points       BIGINT      NOT NULL DEFAULT 0,
    updated_at   DATETIME(6) NOT NULL,
    final_rank   INT         NULL,
    reward_coins INT         NULL,
    PRIMARY KEY (event_id, player_id),
    KEY ix_participation_ranking (event_id, points DESC, updated_at),
    CONSTRAINT fk_participation_event FOREIGN KEY (event_id) REFERENCES live_event (id),
    CONSTRAINT fk_participation_player FOREIGN KEY (player_id) REFERENCES players (id)
) ENGINE = InnoDB;
