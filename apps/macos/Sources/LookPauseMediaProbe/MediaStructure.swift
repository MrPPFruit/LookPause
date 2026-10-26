import AppKit
import ApplicationServices

enum MediaMember: String, Decodable { case container = "Container", playback = "Playback", seek = "Seek", volume = "Volume", action = "Action", time = "Time", foreign = "Foreign" }

func activeMediaDocument(roots: [(Int, String)], windowDocument: String, focusedRoot: Int?) -> Int? {
    guard roots.count == 1, let root = roots.first,
          windowDocument.isEmpty || root.1 == windowDocument || root.0 == focusedRoot else { return nil }
    return root.0
}

// 共用直接同组合同；Mac 原生包装层只有在下方精确结构验证后才能归一化。
func closedMediaCluster(_ members: [(MediaMember, Int)], scope: Int) -> Bool {
    !members.isEmpty && members.allSatisfy { $0.0 != .foreign && $0.1 == scope } &&
    [MediaMember.playback, .seek, .volume].allSatisfy { kind in members.filter { $0.0 == kind }.count == 1 } &&
    members.contains { $0.0 == .action }
}

struct MediaAXNode {
    let element: AXUIElement
    let parent: Int?
    let role: String
    let semantics: String
    let member: MediaMember
    let documents: [Int]
    let namedScope: Int?
    let visible: Bool
}

func mediaDescendants(_ scope: Int, nodes: [MediaAXNode]) -> [Int] {
    var included: Set<Int> = [scope]
    return nodes.indices.filter { index in
        guard index > scope, let parent = nodes[index].parent, included.contains(parent) else { return false }
        included.insert(index)
        return true
    }
}

// 索引同时用于真实 AX 与合成树。只接受实测的控制栏／音量包装层，不递归压平任意页面。
func closedMediaScope(_ scope: Int, nodes: [MediaAXNode]) -> Bool {
    guard nodes[scope].member == .container, nodes[scope].visible else { return false }
    var descendants: [Int] = []
    var included: Set<Int> = [scope]
    for index in nodes.indices where index > scope {
        if let parent = nodes[index].parent, included.contains(parent) {
            included.insert(index); descendants.append(index)
            if descendants.count > 64 { return false }
        }
    }
    guard descendants.count <= 64, descendants.allSatisfy({
        let node = nodes[$0]
        return node.documents == nodes[scope].documents && node.member != .foreign && node.visible
    }) else { return false }
    if closedMediaCluster(descendants.map { (nodes[$0].member, nodes[$0].parent ?? -1) }, scope: scope) { return true }
    let plays = descendants.filter { nodes[$0].member == .playback }
    let seeks = descendants.filter { nodes[$0].member == .seek }
    let volumes = descendants.filter { nodes[$0].member == .volume }
    guard plays.count == 1, seeks.count == 1, volumes.count == 1,
          nodes[seeks[0]].parent == scope, let bar = nodes[plays[0]].parent,
          bar != scope, nodes[bar].parent == scope, nodes[bar].member == .container else { return false }
    let volumeParent = nodes[volumes[0]].parent
    guard volumeParent == bar || volumeParent.map({ nodes[$0].member == .container && nodes[$0].parent == bar }) == true else { return false }
    // 外层只容纳 seek、控制栏及空装饰；动作只能位于控制栏／音量组。
    return descendants.contains { nodes[$0].member == .action && nodes[$0].parent == bar } &&
        descendants.allSatisfy { index in
            let node = nodes[index]
            if node.member == .container { return true }
            if index == seeks[0] || index == plays[0] || index == volumes[0] { return true }
            return node.member == .action && (node.parent == bar || node.parent == volumeParent)
        }
}

struct MediaDocument {
    let element: AXUIElement
    let address: String
}

func sameMediaDocuments(_ left: [MediaDocument], _ right: [MediaDocument]) -> Bool {
    left.count == right.count && zip(left, right).allSatisfy { CFEqual($0.element, $1.element) && $0.address == $1.address }
}

func mediaDocuments(_ element: AXUIElement) -> [MediaDocument]? {
    var chain: [MediaDocument] = []
    var current: AXUIElement? = element
    for _ in 0..<64 {
        guard let node = current else { return nil }
        let role = axAttribute(node, kAXRoleAttribute) as? String ?? ""
        if role == kAXWindowRole { return chain }
        if role == "AXWebArea" { chain.append(MediaDocument(element: node, address: axAddress(node, "AXURL"))) }
        current = axElement(node, kAXParentAttribute)
    }
    return nil
}

func insideMediaScope(_ element: AXUIElement, scope: AXUIElement) -> Bool {
    var current: AXUIElement? = element
    for _ in 0..<64 {
        guard let node = current else { return false }
        if CFEqual(node, scope) { return true }
        if axAttribute(node, kAXRoleAttribute) as? String == kAXWindowRole { return false }
        current = axElement(node, kAXParentAttribute)
    }
    return false
}

func mediaVisible(_ element: AXUIElement, allowCollapsedVolume: Bool = false) -> Bool {
    guard (axAttribute(element, kAXEnabledAttribute) as? NSNumber)?.boolValue != false,
          (axAttribute(element, "AXHidden") as? NSNumber)?.boolValue != true else { return false }
    guard !allowCollapsedVolume else { return true } // 辅助证据，永不提交动作。
    guard let raw = axAttribute(element, kAXSizeAttribute), CFGetTypeID(raw) == AXValueGetTypeID() else { return false }
    var size = CGSize.zero
    return AXValueGetValue(unsafeDowncast(raw, to: AXValue.self), .cgSize, &size) && size.width > 0 && size.height > 0
}

func mediaMember(role: String, label: String, semantics: String) -> MediaMember {
    if [kAXButtonRole, kAXCheckBoxRole].contains(role), mediaHint(label: label) != nil { return .playback }
    if role == kAXSliderRole {
        if semantics.range(of: #"\bseek\b|进度|播放位置|时间线"#, options: .regularExpression) != nil { return .seek }
        if semantics.range(of: #"\bvolume\b|音量"#, options: .regularExpression) != nil { return .volume }
    }
    if [kAXButtonRole, kAXPopUpButtonRole].contains(role),
       label.range(of: #"^(?:进入全屏模式|退出全屏模式|全屏|静音|取消静音|显示更多媒体控件|enter full screen|exit full screen|full screen|mute|unmute|show more media controls)$"#, options: [.regularExpression, .caseInsensitive]) != nil { return .action }
    if role == kAXGroupRole { return .container }
    return .foreign
}
