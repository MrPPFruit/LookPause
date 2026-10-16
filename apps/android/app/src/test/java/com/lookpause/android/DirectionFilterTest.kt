package com.lookpause.android

import org.junit.Assert.assertEquals
import org.junit.Test

class DirectionFilterTest {
    private fun calibrated(): DirectionFilter = DirectionFilter().also { f ->
        repeat(5) { f.observe(Orientation(0f, 0.5f), 1, it * 330L) }
    }
    @Test fun briefGlanceDoesNotPauseButSustainedDepartureDoes() {
        val f = calibrated()
        assertEquals(WatchingIntent.ATTENDING, f.observe(Orientation(0.4f, 0.5f), 1, 1650).intent)
        assertEquals(WatchingIntent.ATTENDING, f.observe(Orientation(0f, 0.5f), 1, 1980).intent)
        for (time in 2310L..3960L step 330) f.observe(Orientation(0.4f, 0.5f), 1, time)
        assertEquals(WatchingIntent.AWAY, f.observe(Orientation(0.4f, 0.5f), 1, 4290).intent)
        assertEquals(WatchingIntent.AWAY, f.observe(Orientation(0f, 0.5f), 1, 4620).intent)
        f.observe(Orientation(0f, 0.5f), 1, 4950)
        assertEquals(WatchingIntent.ATTENDING, f.observe(Orientation(0f, 0.5f), 1, 5280).intent)
    }
    @Test fun missingBaselineMultipleFacesAndObservationGapAreUnknown() {
        assertEquals(WatchingIntent.UNKNOWN, DirectionFilter().observe(null, 0, 3000).intent)
        assertEquals(WatchingIntent.UNKNOWN, calibrated().observe(null, 2, 1650).intent)
        assertEquals(WatchingIntent.UNKNOWN, calibrated().observe(Orientation(0f, 0.5f), 1, 6000).intent)
    }
    @Test fun noFaceMustPersistTwoSeconds() {
        val f = calibrated()
        for (time in 1650L..3300L step 330) assertEquals(WatchingIntent.ATTENDING, f.observe(null, 0, time).intent)
        f.observe(null, 0, 3630)
        assertEquals(WatchingIntent.NO_FACE, f.observe(null, 0, 3960).intent)
    }
}
