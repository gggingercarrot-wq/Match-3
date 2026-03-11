using DG.Tweening;
using StaticData;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(CellFactory))]
public class BoardService : MonoBehaviour
{
    [SerializeField] private Sprite[] _cellSprites;
    [SerializeField] private ParticleSystem _matchFXPrefab;
    [SerializeField] private ScoreService _scoreService;

    public ArrayLayout boardLayout;

    private CellData[,] _board;
    private CellFactory _cellFactory;
    private MatchMachine _matchMachine;
    private CellMover _cellMover;
    private PlaneBooster _planeBooster;

    private readonly List<Cell> _updatingCells = new List<Cell>();
    private readonly List<CellFlip> _flippedCells = new List<CellFlip>();
    private readonly List<Cell> _deadCells = new List<Cell>();
    private readonly int[] _fillingCellsCountByColumn = new int[Config.BoardWidth];
    private readonly List<ParticleSystem> _matchFXs = new List<ParticleSystem>();
    private readonly List<Point> _squarePositions = new List<Point>();

    public Sprite[] CellSprites => _cellSprites;

    private void Awake()
    {
        _cellFactory = GetComponent<CellFactory>();
        _matchMachine = new MatchMachine(this);
        _cellMover = new CellMover(this);
        _planeBooster = new PlaneBooster(this, _cellFactory, _scoreService);
    }

    private void Start()
    {
        InitializeBoard();
        VerifyBoardOnMatches();
        _cellFactory.InstantiateBoard(this, _cellMover);
    }

    private void Update()
    {
        _cellMover.Update();

        var finishedUpdating = new List<Cell>();
        foreach (var cell in _updatingCells)
        {
            if (!cell.UpdateCell())
            {
                finishedUpdating.Add(cell);
            }
        }

        foreach (var cell in finishedUpdating)
        {
            var x = cell.Point.x;
            _fillingCellsCountByColumn[x] = Mathf.Clamp(_fillingCellsCountByColumn[x] - 1, 0, Config.BoardWidth);
            var flip = GetFlip(cell);

            if (flip == null)
            {
                _updatingCells.Remove(cell);

                var connectedPoints = _matchMachine.GetMatchedPoints(cell.Point, true);
                if (connectedPoints.Count >= 2)
                {
                    ParticleSystem matchFX = GetAvailableMatchFX();
                    matchFX.transform.position = cell.rect.transform.position;
                    matchFX.Play();

                    foreach (var connectedPoint in connectedPoints)
                    {
                        _cellFactory.KillCell(connectedPoint);
                        var cellAtPoint = GetCellAtPoint(connectedPoint);
                        var connectedCell = cellAtPoint.GetCell();
                        if (connectedCell != null)
                        {
                            connectedCell.gameObject.SetActive(false);
                            _deadCells.Add(connectedCell);
                        }
                        cellAtPoint.SetCell(null);
                    }

                    _scoreService.AddScore(connectedPoints.Count);

                    foreach (var planePoint in _squarePositions)
                    {
                        var cellAtPlanePoint = GetCellAtPoint(planePoint);
                        if (cellAtPlanePoint != null && cellAtPlanePoint.GetCell() == null)
                        {
                            CreatePlaneAtPoint(planePoint);
                        }
                    }
                    _squarePositions.Clear();

                    ApplyGravityToBoard();
                }
                continue;
            }

            var flippedCell = flip.GetOtherCell(cell);

            var connectedPoints2 = _matchMachine.GetMatchedPoints(cell.Point, true);
            MatchMachine.AddPoints(ref connectedPoints2,
                _matchMachine.GetMatchedPoints(flippedCell.Point, true));

            _flippedCells.Remove(flip);
            _updatingCells.Remove(cell);
            _updatingCells.Remove(flippedCell);

            if (connectedPoints2.Count < 2)
            {
                FlipCells(cell.Point, flippedCell.Point, false);
            }
            else
            {
                ParticleSystem matchFX = GetAvailableMatchFX();
                matchFX.transform.position = cell.rect.transform.position;
                matchFX.Play();

                foreach (var connectedPoint in connectedPoints2)
                {
                    _cellFactory.KillCell(connectedPoint);
                    var cellAtPoint = GetCellAtPoint(connectedPoint);
                    var connectedCell = cellAtPoint.GetCell();
                    if (connectedCell != null)
                    {
                        connectedCell.gameObject.SetActive(false);
                        _deadCells.Add(connectedCell);
                    }
                    cellAtPoint.SetCell(null);
                }
                _scoreService.AddScore(connectedPoints2.Count);

                foreach (var planePoint in _squarePositions)
                {
                    var cellAtPlanePoint = GetCellAtPoint(planePoint);
                    if (cellAtPlanePoint != null && cellAtPlanePoint.GetCell() == null)
                    {
                        CreatePlaneAtPoint(planePoint);
                    }
                }
                _squarePositions.Clear();

                ApplyGravityToBoard();
            }
        }
    }

    // Этот метод принимает два параметра и передает их в PlaneBooster
    public void ActivatePlane(Point planePoint, CellData.CellType swappedCellType)
    {
        if (_planeBooster == null)
        {
            Debug.LogError("PlaneBooster is not initialized!");
            return;
        }
        _planeBooster.ActivatePlane(planePoint, swappedCellType);
    }


    private ParticleSystem GetAvailableMatchFX()
    {
        for (int i = _matchFXs.Count - 1; i >= 0; i--)
        {
            if (_matchFXs[i] == null)
            {
                _matchFXs.RemoveAt(i);
            }
        }

        foreach (var fx in _matchFXs)
        {
            if (fx != null && fx.isStopped)
            {
                return fx;
            }
        }

        var newFX = Instantiate(_matchFXPrefab, transform);
        _matchFXs.Add(newFX);
        return newFX;
    }

    public void RegisterSquareMatch(Point point, List<Point> squarePoints)
    {
        Point planePoint = FindBottomRightPoint(squarePoints);
        if (!_squarePositions.Contains(planePoint))
        {
            _squarePositions.Add(planePoint);
            Debug.Log($"Квадрат найден! Самолетик появится в точке ({planePoint.x}, {planePoint.y})");
        }
    }

    private Point FindBottomRightPoint(List<Point> points)
    {
        if (points == null || points.Count == 0) return Point.zero;
        Point result = points[0];
        foreach (var point in points)
        {
            if (point.x > result.x || (point.x == result.x && point.y > result.y))
            {
                result = point;
            }
        }
        return result;
    }

    private void CreatePlaneAtPoint(Point point)
    {
        Cell cell;
        if (_deadCells.Count > 0)
        {
            var revivedCell = _deadCells[0];
            revivedCell.gameObject.SetActive(true);
            cell = revivedCell;
            _deadCells.RemoveAt(0);
        }
        else
        {
            cell = _cellFactory.InstantiateCell();
        }

        var planeCellData = new CellData(CellData.CellType.Plane, point);
        cell.Initialize(planeCellData, _cellSprites[_cellSprites.Length - 1], _cellMover);
        cell.rect.anchoredPosition = GetBoardPositionFromPoint(point);

        var cellAtPoint = GetCellAtPoint(point);
        cellAtPoint.SetCell(cell);

        cell.transform.DOPunchScale(Vector3.one * 0.5f, 0.5f, 5, 0.5f);

        ParticleSystem matchFX = GetAvailableMatchFX();
        matchFX.transform.position = cell.rect.transform.position;
        matchFX.Play();
    }

    public void ApplyGravityToBoard()
    {
        for (int x = 0; x < Config.BoardWidth; x++)
        {
            for (int y = Config.BoardHeight - 1; y >= 0; y--)
            {
                var point = new Point(x, y);
                var cellData = GetCellAtPoint(point);
                var cellTypeAtPoint = GetCellTypeAtPoint(point);

                if (cellTypeAtPoint != 0)
                {
                    continue;
                }

                for (int newY = y - 1; newY >= -1; newY--)
                {
                    var nextPoint = new Point(x, newY);
                    var nextCellType = GetCellTypeAtPoint(nextPoint);
                    if (nextCellType == 0)
                        continue;
                    if (nextCellType != CellData.CellType.Hole)
                    {
                        var cellAtPoint = GetCellAtPoint(nextPoint);
                        var cell = cellAtPoint.GetCell();

                        cellData.SetCell(cell);
                        _updatingCells.Add(cell);

                        cellAtPoint.SetCell(null);
                    }
                    else
                    {
                        var cellType = GetRandomCellType();
                        var fallPoint = new Point(x, -1 - _fillingCellsCountByColumn[x]);
                        Cell cell;
                        if (_deadCells.Count > 0)
                        {
                            var revivedCell = _deadCells[0];
                            revivedCell.gameObject.SetActive(true);
                            cell = revivedCell;
                            _deadCells.RemoveAt(0);
                        }
                        else
                        {
                            cell = _cellFactory.InstantiateCell();
                        }

                        cell.Initialize(new CellData(cellType, point), _cellSprites[(int)(cellType - 1)], _cellMover);
                        cell.rect.anchoredPosition = GetBoardPositionFromPoint(fallPoint);

                        var holeCell = GetCellAtPoint(point);
                        holeCell.SetCell(cell);
                        ResetCell(cell);
                        _fillingCellsCountByColumn[x]++;

                    }
                    break;
                }
            }
        }
    }

    public void FlipCells(Point firstPoint, Point secondPoint, bool main)
    {
        if (GetCellTypeAtPoint(firstPoint) < 0)
        {
            return;
        }
        var firstCellData = GetCellAtPoint(firstPoint);
        var firstCell = firstCellData.GetCell();
        if (GetCellTypeAtPoint(secondPoint) > 0)
        {
            var secondCellData = GetCellAtPoint(secondPoint);
            var secondCell = secondCellData.GetCell();
            firstCellData.SetCell(secondCell);
            secondCellData.SetCell(firstCell);

            if (main)
            {
                _flippedCells.Add(new CellFlip(firstCell, secondCell));
                _updatingCells.Add(firstCell);
                _updatingCells.Add(secondCell);
            }
            else
            {
                ResetCell(firstCell);
                ResetCell(secondCell);
            }
        }
    }

    private CellFlip GetFlip(Cell cell)
    {
        foreach (var flip in _flippedCells)
        {
            if (flip.GetOtherCell(cell) != null)
            {
                return flip;
            }
        }
        return null;
    }

    public void ResetCell(Cell cell)
    {
        cell.ResetPosition();
        _updatingCells.Add(cell);
    }

    private void VerifyBoardOnMatches()
    {
        for (int y = 0; y < Config.BoardHeight; y++)
        {
            for (int x = 0; x < Config.BoardWidth; x++)
            {
                var point = new Point(x, y);
                var cellTypeAtPoint = GetCellTypeAtPoint(point);
                if (cellTypeAtPoint <= 0)
                    continue;
                var removeCellTypes = new List<CellData.CellType>();

                while (true)
                {
                    var matches = _matchMachine.GetMatchedPoints(point, true);
                    if (matches.Count <= 2) break;

                    var currentType = GetCellTypeAtPoint(point);
                    removeCellTypes.Add(currentType);
                    var newType = GetNewCellType(removeCellTypes);

                    if (newType == CellData.CellType.Blank)
                        break;

                    SetCellTypeAtPoint(point, newType);
                }
            }
        }
    }

    private void SetCellTypeAtPoint(Point point, CellData.CellType newCellType)
    {
        _board[point.x, point.y].cellType = newCellType;
    }

    private CellData.CellType GetNewCellType(List<CellData.CellType> removeCellTypes)
    {
        var availableCellTypes = new List<CellData.CellType>();
        for (int i = 0; i < CellSprites.Length - 1; i++)
            availableCellTypes.Add((CellData.CellType)i + 1);

        availableCellTypes.RemoveAll(type => removeCellTypes.Contains(type));
        return availableCellTypes.Count == 0 ? CellData.CellType.Blank : availableCellTypes[Random.Range(0, availableCellTypes.Count)];
    }

    public CellData.CellType GetCellTypeAtPoint(Point point)
    {
        if (point.x < 0 || point.x >= Config.BoardWidth
            || point.y < 0 || point.y >= Config.BoardHeight)
            return CellData.CellType.Hole;
        return _board[point.x, point.y].cellType;
    }

    private void InitializeBoard()
    {
        _board = new CellData[Config.BoardWidth, Config.BoardHeight];
        for (int y = 0; y < Config.BoardHeight; y++)
        {
            for (int x = 0; x < Config.BoardWidth; x++)
            {
                _board[x, y] = new CellData(boardLayout.rows[y].row[x] ? CellData.CellType.Hole : GetRandomCellType(),
                new Point(x, y));
            }
        }
    }

    private CellData.CellType GetRandomCellType()
    {
        return (CellData.CellType)(Random.Range(0, _cellSprites.Length - 1) + 1);
    }

    public CellData GetCellAtPoint(Point point)
        => _board[point.x, point.y];

    public static Vector2 GetBoardPositionFromPoint(Point point)
    {
        return new Vector2(
            Config.PieceSize / 2 + Config.PieceSize * point.x,
            -Config.PieceSize / 2 - Config.PieceSize * point.y
            );
    }
}