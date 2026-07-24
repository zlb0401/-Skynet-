#pragma once

#include <cstdint>
#include <mutex>
#include <string>
#include <unordered_map>

/// Simple math captcha store shared across auth connections.
class CaptchaStore {
public:
    struct Challenge {
        std::string question;
        int answer = 0;
        int64_t expires_at = 0;
    };

    static CaptchaStore& instance();

    /// Returns captcha_id and question text (e.g. "7 + 4 = ?").
    std::pair<std::string, std::string> create();

    /// One-time consume. Returns false if missing / expired / wrong.
    bool consume(const std::string& id, const std::string& answer_text);

private:
    CaptchaStore() = default;
    void purge_locked(int64_t now);

    std::mutex mu_;
    std::unordered_map<std::string, Challenge> map_;
};
