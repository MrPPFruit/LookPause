import AppKit

// 只投影连接和方向，不表示播放器状态，也不参与媒体裁决。
enum DesktopStatus: String, CaseIterable {
    case disconnected, waiting, attending, away

    init(connected: Bool, watching: Bool, intent: WatchingIntent) {
        guard connected else { self = .disconnected; return }
        guard watching else { self = .waiting; return }
        switch intent {
        case .attending: self = .attending
        case .away, .noFace: self = .away
        case .unknown: self = .waiting
        }
    }

    var label: String {
        switch self {
        case .disconnected: "手机未连接"
        case .waiting: "已连接，等待观看方向"
        case .attending: "已连接，正在看屏幕"
        case .away: "已连接，未看屏幕"
        }
    }

    func image() -> NSImage {
        let image = NSImage(size: NSSize(width: 20, height: 20), flipped: true) { rect in
            let transform = NSAffineTransform()
            transform.translateX(by: rect.minX, yBy: rect.minY)
            transform.scaleX(by: rect.width / 20, yBy: rect.height / 20)
            transform.concat()
            let dark = NSAppearance.currentDrawing().bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
            let color: NSColor = self == .disconnected ? .labelColor :
                (dark ? NSColor(srgbRed: 0.31, green: 0.88, blue: 0.56, alpha: 1) :
                        NSColor(srgbRed: 0.07, green: 0.43, blue: 0.24, alpha: 1))
            color.set()
            let path = NSBezierPath()
            path.lineWidth = 1.8
            path.lineCapStyle = .round
            path.lineJoinStyle = .round
            switch self {
            case .disconnected:
                path.move(to: NSPoint(x: 8, y: 5))
                path.curve(to: NSPoint(x: 3, y: 10), controlPoint1: NSPoint(x: 1, y: 1), controlPoint2: NSPoint(x: 0, y: 7))
                path.line(to: NSPoint(x: 5, y: 12))
                path.move(to: NSPoint(x: 12, y: 15))
                path.curve(to: NSPoint(x: 17, y: 10), controlPoint1: NSPoint(x: 19, y: 19), controlPoint2: NSPoint(x: 20, y: 13))
                path.line(to: NSPoint(x: 15, y: 8))
                path.move(to: NSPoint(x: 10, y: 3)); path.line(to: NSPoint(x: 11, y: 1))
                path.move(to: NSPoint(x: 10, y: 17)); path.line(to: NSPoint(x: 9, y: 19))
                path.stroke()
            case .waiting, .attending:
                path.move(to: NSPoint(x: 1.5, y: 10))
                path.curve(to: NSPoint(x: 18.5, y: 10), controlPoint1: NSPoint(x: 6, y: 2), controlPoint2: NSPoint(x: 14, y: 2))
                path.curve(to: NSPoint(x: 1.5, y: 10), controlPoint1: NSPoint(x: 14, y: 18), controlPoint2: NSPoint(x: 6, y: 18))
                path.close()
                if self == .attending {
                    path.appendOval(in: NSRect(x: 7.5, y: 7.5, width: 5, height: 5))
                    path.windingRule = .evenOdd
                    path.fill()
                } else { path.stroke() }
            case .away:
                path.move(to: NSPoint(x: 2, y: 7))
                path.curve(to: NSPoint(x: 18, y: 7), controlPoint1: NSPoint(x: 6, y: 14), controlPoint2: NSPoint(x: 14, y: 14))
                path.move(to: NSPoint(x: 5, y: 10.5)); path.line(to: NSPoint(x: 3.5, y: 13.5))
                path.move(to: NSPoint(x: 10, y: 12.3)); path.line(to: NSPoint(x: 10, y: 16))
                path.move(to: NSPoint(x: 15, y: 10.5)); path.line(to: NSPoint(x: 16.5, y: 13.5))
                path.stroke()
            }
            return true
        }
        image.isTemplate = self == .disconnected
        image.accessibilityDescription = label
        return image
    }
}
