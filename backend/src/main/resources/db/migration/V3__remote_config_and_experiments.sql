-- Remote configuration: key/value tuning knobs editable from the admin panel at runtime.
CREATE TABLE remote_config (
    config_key   VARCHAR(64)  NOT NULL,
    config_value JSON         NOT NULL,
    description  VARCHAR(255) NULL,
    updated_at   DATETIME(6)  NOT NULL,
    updated_by   VARCHAR(32)  NULL,
    PRIMARY KEY (config_key)
) ENGINE = InnoDB;

INSERT INTO remote_config (config_key, config_value, description, updated_at, updated_by) VALUES
    ('dailyRewardCoins',        '100',  'Coins granted by the daily reward',                          UTC_TIMESTAMP(6), 'seed'),
    ('dailyRewardStreakBonus',  '25',   'Extra coins per consecutive day (capped at 7 days)',         UTC_TIMESTAMP(6), 'seed'),
    ('maxLives',                '5',    'Maximum number of lives',                                    UTC_TIMESTAMP(6), 'seed'),
    ('lifeRegenerationMinutes', '30',   'Minutes to regenerate one life',                             UTC_TIMESTAMP(6), 'seed'),
    ('lifeRefillPrice',         '150',  'Coin price of a full life refill',                           UTC_TIMESTAMP(6), 'seed'),
    ('boosterPrices',           '{"HAMMER": 100, "SHUFFLE": 80, "EXTRA_MOVES": 120}', 'Coin price per booster', UTC_TIMESTAMP(6), 'seed'),
    ('startingCoins',           '500',  'Coins a new account starts with',                            UTC_TIMESTAMP(6), 'seed'),
    ('levelCompleteBaseCoins',  '50',   'Base coins for clearing a level',                            UTC_TIMESTAMP(6), 'seed'),
    ('coinsPerStar',            '25',   'Additional coins per star earned',                           UTC_TIMESTAMP(6), 'seed'),
    ('firstClearBonusCoins',    '50',   'Bonus for clearing a level for the first time',              UTC_TIMESTAMP(6), 'seed'),
    ('rewardMultiplier',        '1.0',  'Global multiplier applied to level rewards',                 UTC_TIMESTAMP(6), 'seed'),
    ('rocketRaceEnabled',       'true', 'Feature flag for the Rocket Race live event',                UTC_TIMESTAMP(6), 'seed'),
    ('leaderboardEnabled',      'true', 'Feature flag for the weekly leaderboard',                    UTC_TIMESTAMP(6), 'seed');

-- A/B experiments: definition + sticky per-player assignments.
CREATE TABLE experiment (
    id             BIGINT       NOT NULL AUTO_INCREMENT,
    experiment_key VARCHAR(64)  NOT NULL,
    name           VARCHAR(128) NOT NULL,
    status         VARCHAR(16)  NOT NULL DEFAULT 'DRAFT',
    start_at       DATETIME(6)  NULL,
    end_at         DATETIME(6)  NULL,
    variants       JSON         NOT NULL,
    created_at     DATETIME(6)  NOT NULL,
    updated_at     DATETIME(6)  NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uk_experiment_key (experiment_key),
    KEY ix_experiment_status (status)
) ENGINE = InnoDB;

CREATE TABLE experiment_assignment (
    experiment_id BIGINT      NOT NULL,
    player_id     BIGINT      NOT NULL,
    variant       VARCHAR(32) NOT NULL,
    bucket        INT         NOT NULL,
    assigned_at   DATETIME(6) NOT NULL,
    PRIMARY KEY (experiment_id, player_id),
    KEY ix_assignment_player (player_id),
    CONSTRAINT fk_assignment_experiment FOREIGN KEY (experiment_id) REFERENCES experiment (id),
    CONSTRAINT fk_assignment_player FOREIGN KEY (player_id) REFERENCES players (id)
) ENGINE = InnoDB;
