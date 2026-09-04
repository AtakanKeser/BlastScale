package com.atakan.blastscale.economy;

import jakarta.persistence.LockModeType;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.Optional;

public interface WalletRepository extends JpaRepository<Wallet, Long> {

    /**
     * {@code SELECT ... FOR UPDATE}: serialises every balance change of one player. Two requests
     * for the same wallet queue up on the MySQL row lock; requests for different players never
     * wait for each other.
     */
    @Lock(LockModeType.PESSIMISTIC_WRITE)
    @Query("select w from Wallet w where w.playerId = :playerId")
    Optional<Wallet> lockByPlayerId(@Param("playerId") long playerId);
}
