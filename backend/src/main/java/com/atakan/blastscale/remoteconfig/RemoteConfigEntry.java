package com.atakan.blastscale.remoteconfig;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.type.SqlTypes;

import java.time.Instant;

/** One remote configuration key. The value is any JSON scalar/object/array. */
@Entity
@Table(name = "remote_config")
public class RemoteConfigEntry {

    @Id
    @Column(name = "config_key", length = 64)
    private String key;

    @JdbcTypeCode(SqlTypes.JSON)
    @Column(name = "config_value", nullable = false, columnDefinition = "json")
    private String valueJson;

    @Column(name = "description", length = 255)
    private String description;

    @Column(name = "updated_at", nullable = false)
    private Instant updatedAt;

    @Column(name = "updated_by", length = 32)
    private String updatedBy;

    protected RemoteConfigEntry() {
        // JPA
    }

    public RemoteConfigEntry(String key, String valueJson, String description, Instant now, String updatedBy) {
        this.key = key;
        this.valueJson = valueJson;
        this.description = description;
        this.updatedAt = now;
        this.updatedBy = updatedBy;
    }

    public String getKey() {
        return key;
    }

    public String getValueJson() {
        return valueJson;
    }

    public String getDescription() {
        return description;
    }

    public Instant getUpdatedAt() {
        return updatedAt;
    }

    public String getUpdatedBy() {
        return updatedBy;
    }

    public void update(String valueJson, String description, Instant now, String updatedBy) {
        this.valueJson = valueJson;
        if (description != null) {
            this.description = description;
        }
        this.updatedAt = now;
        this.updatedBy = updatedBy;
    }
}
