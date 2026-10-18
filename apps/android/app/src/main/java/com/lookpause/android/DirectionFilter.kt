package com.lookpause.android

import kotlin.math.abs

enum class WatchingIntent { ATTENDING, AWAY, NO_FACE, UNKNOWN }
data class Orientation(val x: Float, val y: Float)
data class Observation(val intent: WatchingIntent, val description: String)

// shortcut: 低成本头部方向代理不等同眼动追踪；初值需通过 S25 位置、光照实测调整。
class DirectionFilter {
    private val calibration = ArrayDeque<Orientation>()
    private var baseline: Orientation? = null
    private var candidate = WatchingIntent.UNKNOWN
    private var since = 0L
    private var lastFrame = -1L
    private var confirmed = WatchingIntent.UNKNOWN

    fun observe(pose: Orientation?, faceCount: Int, now: Long): Observation {
        if (lastFrame >= 0 && now - lastFrame > 1500) {
            candidate = WatchingIntent.UNKNOWN; confirmed = WatchingIntent.UNKNOWN; since = now
        }
        lastFrame = now
        if (faceCount > 1 || (faceCount == 1 && pose == null)) {
            calibration.clear(); candidate = WatchingIntent.UNKNOWN; confirmed = WatchingIntent.UNKNOWN
            return Observation(WatchingIntent.UNKNOWN, "当前方向不明确，保持视频状态")
        }
        if (baseline == null) {
            if (pose == null) {
                calibration.clear()
                return Observation(WatchingIntent.UNKNOWN, "未检测到人脸，请看向电脑屏幕建立基准")
            }
            calibration.add(pose)
            if (calibration.size > 5) calibration.removeFirst()
            if (calibration.size == 5 && calibration.maxOf { it.x } - calibration.minOf { it.x } < 0.08f &&
                calibration.maxOf { it.y } - calibration.minOf { it.y } < 0.08f) {
                baseline = Orientation(calibration.map { it.x }.average().toFloat(), calibration.map { it.y }.average().toFloat())
                confirmed = WatchingIntent.ATTENDING
                candidate = confirmed; since = now; calibration.clear()
                return Observation(confirmed, "看向屏幕")
            }
            return Observation(WatchingIntent.UNKNOWN, "请短暂看向电脑屏幕，正在建立基准")
        }
        val origin = baseline!!
        val raw = if (faceCount == 0) WatchingIntent.NO_FACE
            else if (abs(pose!!.x - origin.x) > 0.18f || abs(pose.y - origin.y) > 0.18f) WatchingIntent.AWAY
            else WatchingIntent.ATTENDING
        if (candidate != raw) { candidate = raw; since = now }
        val wait = when (raw) { WatchingIntent.AWAY -> 1500; WatchingIntent.NO_FACE -> 2000; else -> 600 }
        if (now - since >= wait) confirmed = raw
        return Observation(confirmed, when (confirmed) {
            WatchingIntent.ATTENDING -> "看向屏幕"
            WatchingIntent.AWAY -> "已离开观影方向"
            WatchingIntent.NO_FACE -> "未检测到人脸"
            WatchingIntent.UNKNOWN -> "方向暂不明确，保持视频状态"
        })
    }
}
