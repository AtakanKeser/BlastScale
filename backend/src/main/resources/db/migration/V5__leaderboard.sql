-- Weekly leaderboard finalization bookkeeping. Scores themselves live in a Redis sorted set;
-- these tables record which seasons were closed and who was paid, so the job is idempotent.
CREATE TABLE leaderboard_season (
    season           VARCHAR(10) NOT NULL,
    finalized_at     DATETIME(6) NOT NULL,
    participants     INT         NOT NULL,
    rewarded_players INT         NOT NULL,
    PRIMARY KEY (season)
) ENGINE = InnoDB;

CREATE TABLE leaderboard_reward (
    season        VARCHAR(10) NOT NULL,
    player_id     BIGINT      NOT NULL,
    rank_position INT         NOT NULL,
    score         BIGINT      NOT NULL,
    coins         INT         NOT NULL,
    PRIMARY KEY (season, player_id),
    CONSTRAINT fk_lb_reward_player FOREIGN KEY (player_id) REFERENCES players (id)
) ENGINE = InnoDB;
