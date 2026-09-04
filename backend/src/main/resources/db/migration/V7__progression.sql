-- One row per level attempt. The status column is the exactly-once lock for completion:
-- "UPDATE ... WHERE status = 'ACTIVE'" succeeds for exactly one request.
CREATE TABLE game_session (
    id                    CHAR(36)    NOT NULL,
    player_id             BIGINT      NOT NULL,
    level_id              INT         NOT NULL,
    seed                  INT         NOT NULL,
    configuration_version INT         NOT NULL,
    status                VARCHAR(16) NOT NULL,
    started_at            DATETIME(6) NOT NULL,
    completed_at          DATETIME(6) NULL,
    score                 INT         NULL,
    moves_used            INT         NULL,
    stars                 INT         NULL,
    reward_coins          BIGINT      NULL,
    reward_strategy       VARCHAR(32) NULL,
    reward_multiplier     DOUBLE      NULL,
    PRIMARY KEY (id),
    KEY ix_session_player_started (player_id, started_at),
    KEY ix_session_status_started (status, started_at),
    CONSTRAINT fk_session_player FOREIGN KEY (player_id) REFERENCES players (id)
) ENGINE = InnoDB;

-- Best result per player and level (what the level map shows).
CREATE TABLE level_progress (
    player_id      BIGINT      NOT NULL,
    level_id       INT         NOT NULL,
    stars          INT         NOT NULL DEFAULT 0,
    best_score     INT         NOT NULL DEFAULT 0,
    attempts       INT         NOT NULL DEFAULT 0,
    completed_at   DATETIME(6) NULL,
    last_played_at DATETIME(6) NOT NULL,
    PRIMARY KEY (player_id, level_id),
    CONSTRAINT fk_progress_player FOREIGN KEY (player_id) REFERENCES players (id)
) ENGINE = InnoDB;
