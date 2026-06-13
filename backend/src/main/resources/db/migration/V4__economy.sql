-- Wallet: current balances, one row per player, always modified under SELECT ... FOR UPDATE.
CREATE TABLE player_wallet (
    player_id        BIGINT      NOT NULL,
    coins            BIGINT      NOT NULL DEFAULT 0,
    lives            INT         NOT NULL DEFAULT 0,
    stars            INT         NOT NULL DEFAULT 0,
    lives_updated_at DATETIME(6) NOT NULL,
    version          BIGINT      NOT NULL DEFAULT 0,
    PRIMARY KEY (player_id),
    CONSTRAINT fk_wallet_player FOREIGN KEY (player_id) REFERENCES players (id)
) ENGINE = InnoDB;

CREATE TABLE player_booster (
    id           BIGINT      NOT NULL AUTO_INCREMENT,
    player_id    BIGINT      NOT NULL,
    booster_type VARCHAR(32) NOT NULL,
    quantity     INT         NOT NULL DEFAULT 0,
    PRIMARY KEY (id),
    UNIQUE KEY uk_booster_player_type (player_id, booster_type),
    CONSTRAINT fk_booster_player FOREIGN KEY (player_id) REFERENCES players (id)
) ENGINE = InnoDB;

-- Append-only ledger. The unique key is the database-level exactly-once guarantee for rewards:
-- a second LEVEL_COMPLETE for the same session, or a second DAILY_REWARD for the same day,
-- cannot be inserted no matter what the application layer does.
CREATE TABLE economy_transaction (
    id            BIGINT      NOT NULL AUTO_INCREMENT,
    player_id     BIGINT      NOT NULL,
    type          VARCHAR(8)  NOT NULL,
    resource      VARCHAR(32) NOT NULL,
    amount        BIGINT      NOT NULL,
    balance_after BIGINT      NOT NULL,
    reason        VARCHAR(32) NOT NULL,
    reference_id  VARCHAR(64) NOT NULL,
    created_at    DATETIME(6) NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uk_tx_player_reason_reference_resource (player_id, reason, reference_id, resource),
    KEY ix_tx_player_created (player_id, created_at),
    KEY ix_tx_reason_reference (reason, reference_id),
    CONSTRAINT fk_tx_player FOREIGN KEY (player_id) REFERENCES players (id)
) ENGINE = InnoDB;

-- One row per player and UTC day; the primary key makes double claims impossible.
CREATE TABLE daily_reward_claim (
    player_id  BIGINT      NOT NULL,
    claimed_on DATE        NOT NULL,
    streak     INT         NOT NULL,
    coins      INT         NOT NULL,
    claimed_at DATETIME(6) NOT NULL,
    PRIMARY KEY (player_id, claimed_on),
    CONSTRAINT fk_daily_player FOREIGN KEY (player_id) REFERENCES players (id)
) ENGINE = InnoDB;
