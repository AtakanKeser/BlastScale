package com.atakan.blastscale.experiment;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;
import java.util.Optional;

public interface ExperimentRepository extends JpaRepository<Experiment, Long> {

    Optional<Experiment> findByKey(String key);

    List<Experiment> findByStatus(ExperimentStatus status);

    List<Experiment> findAllByOrderByIdDesc();
}
