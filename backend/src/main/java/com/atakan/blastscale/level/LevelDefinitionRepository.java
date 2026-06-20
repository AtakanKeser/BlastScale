package com.atakan.blastscale.level;

import org.springframework.data.mongodb.repository.MongoRepository;

import java.util.List;

public interface LevelDefinitionRepository extends MongoRepository<LevelDefinition, String> {

    List<LevelDefinition> findByLevelNumberBetweenOrderByLevelNumberAsc(int from, int to);
}
