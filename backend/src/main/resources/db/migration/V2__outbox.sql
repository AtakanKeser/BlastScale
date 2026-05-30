-- Transactional outbox: written in the same transaction as the business change,
-- drained asynchronously into Elasticsearch by OutboxPublisherJob.
CREATE TABLE outbox_event (
    id             BIGINT       NOT NULL AUTO_INCREMENT,
    event_type     VARCHAR(48)  NOT NULL,
    player_id      BIGINT       NULL,
    aggregate_type VARCHAR(32)  NOT NULL,
    aggregate_id   VARCHAR(64)  NOT NULL,
    payload        JSON         NOT NULL,
    created_at     DATETIME(6)  NOT NULL,
    published_at   DATETIME(6)  NULL,
    attempts       INT          NOT NULL DEFAULT 0,
    last_error     VARCHAR(512) NULL,
    PRIMARY KEY (id),
    -- the publisher scans "published_at IS NULL ORDER BY id"; this index keeps that cheap
    KEY ix_outbox_unpublished (published_at, id)
) ENGINE = InnoDB;
