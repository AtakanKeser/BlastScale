package com.atakan.blastscale.security;

import org.springframework.context.annotation.Configuration;
import org.springframework.web.method.support.HandlerMethodArgumentResolver;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;

import java.util.List;

/** Registers the {@link CurrentPlayerArgumentResolver} with Spring MVC. */
@Configuration
public class WebMvcConfig implements WebMvcConfigurer {

    private final CurrentPlayerArgumentResolver currentPlayerArgumentResolver;

    public WebMvcConfig(CurrentPlayerArgumentResolver currentPlayerArgumentResolver) {
        this.currentPlayerArgumentResolver = currentPlayerArgumentResolver;
    }

    @Override
    public void addArgumentResolvers(List<HandlerMethodArgumentResolver> resolvers) {
        resolvers.add(currentPlayerArgumentResolver);
    }
}
