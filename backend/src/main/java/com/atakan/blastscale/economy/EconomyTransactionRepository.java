package com.atakan.blastscale.economy;

import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

public interface EconomyTransactionRepository extends JpaRepository<EconomyTransaction, Long> {

    boolean existsByPlayerIdAndReasonAndReferenceId(long playerId, TransactionReason reason, String referenceId);

    Page<EconomyTransaction> findByPlayerIdOrderByIdDesc(long playerId, Pageable pageable);

    List<EconomyTransaction> findByPlayerIdAndReasonAndReferenceId(long playerId, TransactionReason reason, String referenceId);

    long countByReasonAndReferenceId(TransactionReason reason, String referenceId);
}
