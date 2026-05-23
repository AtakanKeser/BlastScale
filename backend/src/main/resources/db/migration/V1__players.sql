-- Player accounts. Guests have a device_id and no password; registered players the opposite.
CREATE TABLE players (
    id            BIGINT       NOT NULL AUTO_INCREMENT,
    username      VARCHAR(32)  NOT NULL,
    password_hash VARCHAR(100) NULL,
    device_id     VARCHAR(128) NULL,
    role          VARCHAR(16)  NOT NULL DEFAULT 'PLAYER',
    current_level INT          NOT NULL DEFAULT 1,
    created_at    DATETIME(6)  NOT NULL,
    last_seen_at  DATETIME(6)  NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uk_players_username (username),
    UNIQUE KEY uk_players_device_id (device_id)
) ENGINE = InnoDB;
