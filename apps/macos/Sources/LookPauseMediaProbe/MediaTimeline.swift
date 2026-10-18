import Foundation

// 时间线是暂停位置的可观察证据，不把静止或缺失时间线解释为暂停。
struct MediaTimeline: Equatable {
    let position: Double
    let duration: Double
    let rounded: Bool

    var beforeEnd: Bool {
        position.isFinite && duration.isFinite && position >= 0 && duration > 0 && position < duration - 0.05
    }

    func sameDuration(_ other: MediaTimeline) -> Bool {
        duration == other.duration && rounded == other.rounded
    }

    func holdsPause(_ other: MediaTimeline) -> Bool {
        beforeEnd && other.beforeEnd && sameDuration(other) &&
        abs(position - other.position) <= (rounded ? 0 : 0.05)
    }

    static func parse(_ text: String) -> MediaTimeline? {
        let slash = text.components(separatedBy: "/")
        if slash.count == 2, let current = clock(slash[0]), let end = clock(slash[1]), end > 0 {
            return MediaTimeline(position: current, duration: end, rounded: true)
        }
        let parts = text.components(separatedBy: " of ")
        if parts.count == 2, let current = spoken(parts[0]), let end = spoken(parts[1]), end > 0 {
            return MediaTimeline(position: current, duration: end, rounded: true)
        }
        return nil
    }

    private static func clock(_ text: String) -> Double? {
        let units = text.trimmingCharacters(in: .whitespacesAndNewlines).split(separator: ":", omittingEmptySubsequences: false)
        let values = units.compactMap { Double($0) }
        guard (2...3).contains(units.count), values.count == units.count,
              values.allSatisfy({ $0.isFinite && $0 >= 0 }), values.dropFirst().allSatisfy({ $0 < 60 }) else { return nil }
        return values.reduce(0) { $0 * 60 + $1 }
    }

    private static func spoken(_ text: String) -> Double? {
        var total = 0.0
        var found = false
        for (pattern, scale) in [(#"(\d+(?:\.\d+)?)\s*(?:Hours?|小时)"#, 3600.0),
                                 (#"(\d+(?:\.\d+)?)\s*(?:Minutes?|分)"#, 60.0),
                                 (#"(\d+(?:\.\d+)?)\s*(?:Seconds?|秒)"#, 1.0)] {
            let regex = try! NSRegularExpression(pattern: pattern, options: .caseInsensitive)
            if let match = regex.firstMatch(in: text, range: NSRange(text.startIndex..., in: text)),
               let range = Range(match.range(at: 1), in: text), let amount = Double(text[range]) {
                total += amount * scale; found = true
            }
        }
        return found && total.isFinite ? total : nil
    }
}
