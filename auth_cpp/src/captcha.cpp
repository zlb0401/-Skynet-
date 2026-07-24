#include "captcha.h"

#include <algorithm>
#include <chrono>
#include <cstdlib>
#include <random>
#include <sstream>

namespace {

int64_t now_sec() {
    using namespace std::chrono;
    return duration_cast<seconds>(system_clock::now().time_since_epoch()).count();
}

std::string random_id(std::mt19937& rng) {
    static const char* kHex = "0123456789abcdef";
    std::string s(16, '0');
    std::uniform_int_distribution<int> dist(0, 15);
    for (char& c : s) {
        c = kHex[dist(rng)];
    }
    return s;
}

}  // namespace

CaptchaStore& CaptchaStore::instance() {
    static CaptchaStore store;
    return store;
}

void CaptchaStore::purge_locked(int64_t now) {
    for (auto it = map_.begin(); it != map_.end();) {
        if (it->second.expires_at < now) {
            it = map_.erase(it);
        } else {
            ++it;
        }
    }
}

std::pair<std::string, std::string> CaptchaStore::create() {
    thread_local std::mt19937 rng{std::random_device{}()};
    std::uniform_int_distribution<int> num(1, 9);
    const int a = num(rng);
    const int b = num(rng);
    // Mix + and - so it's not always the same pattern.
    std::uniform_int_distribution<int> op(0, 1);
    const bool plus = op(rng) == 0;
    int answer = 0;
    std::ostringstream q;
    if (plus) {
        answer = a + b;
        q << a << " + " << b << " = ?";
    } else {
        // Keep non-negative.
        const int hi = std::max(a, b);
        const int lo = std::min(a, b);
        answer = hi - lo;
        q << hi << " - " << lo << " = ?";
    }

    const int64_t now = now_sec();
    std::lock_guard<std::mutex> lock(mu_);
    purge_locked(now);

    std::string id = random_id(rng);
    while (map_.count(id) != 0) {
        id = random_id(rng);
    }
    map_[id] = Challenge{q.str(), answer, now + 120};
    return {id, q.str()};
}

bool CaptchaStore::consume(const std::string& id, const std::string& answer_text) {
    if (id.empty()) {
        return false;
    }
    char* end = nullptr;
    const long got = std::strtol(answer_text.c_str(), &end, 10);
    if (end == answer_text.c_str() || (end && *end != '\0')) {
        return false;
    }

    const int64_t now = now_sec();
    std::lock_guard<std::mutex> lock(mu_);
    purge_locked(now);
    const auto it = map_.find(id);
    if (it == map_.end()) {
        return false;
    }
    const bool ok = (static_cast<int>(got) == it->second.answer);
    map_.erase(it);  // one-time
    return ok;
}
