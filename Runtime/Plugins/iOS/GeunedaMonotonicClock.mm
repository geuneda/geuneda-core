// Geuneda.Core 단조 시계의 iOS 구현(MonotonicClock.cs)
// Darwin 의 CLOCK_MONOTONIC 은 기기가 잠든 동안에도 흐르고 기기 시계 변경에 영향받지 않는다
#include <stdint.h>
#include <time.h>

extern "C" int64_t GeunedaMonotonicClock_Nanoseconds()
{
    return (int64_t)clock_gettime_nsec_np(CLOCK_MONOTONIC);
}
